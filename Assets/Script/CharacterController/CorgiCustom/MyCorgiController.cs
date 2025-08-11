using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 最適化されたCorgiController実装
    /// Burst Compile、SIMD命令、stackallocを活用して高速化
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Core/My Corgi Controller")]
    public unsafe class MyCorgiController : CorgiController
    {
        // キャッシュ用構造体（SIMD最適化）
        private struct RaycastCache
        {
            public float2 horizontalFromBottom;
            public float2 horizontalToTop;
            public float2 verticalFromLeft;
            public float2 verticalToRight;
            public float2 aboveStart;
            public float2 aboveEnd;
        }

        // Burst対応の計算用構造体（数値データのみ）
        [StructLayout(LayoutKind.Sequential)]
        private struct OptimizationData
        {
            public float2 speed;
            public float2 externalForce;
            public float2 newPosition;
            public float friction;
            public float currentGravity;
            public float deltaTime;
        }

        private RaycastCache _raycastCache;

        // レイキャスト結果保存用配列
        private RaycastHit2D[] _optimizedBelowHitsStorage;

        /// <summary>
        /// 毎フレーム実行される処理（最適化版）
        /// </summary>
        protected override void EveryFrame()
        {
            // タイムスケールチェックを高速化
            if ( Time.timeScale == 0f )
                return;

            // 重力適用（SIMD最適化）
            ApplyGravity();

            // フレーム初期化
            FrameInitialization();

            // レイキャスト処理
            SetRaysParameters();

            // 移動プラットフォーム処理
            HandleMovingPlatforms(_movingPlatform);
            HandleMovingPlatforms(_pusherPlatform, true);

            ForcesApplied = _speed;

            // レイキャスト実行
            DetermineMovementDirection();

            if ( CastRaysOnBothSides )
            {
                CastRaysToTheLeft();
                CastRaysToTheRight();
            }
            else
            {
                if ( _movementDirection == -1 )
                {
                    CastRaysToTheLeft();
                }
                else
                {
                    CastRaysToTheRight();
                }
            }

            CastRaysBelow();
            CastRaysAbove();

            // Transform移動
            MoveTransform();

            // 後処理
            SetRaysParameters();
            ComputeNewSpeed();
            SetStates();
            ComputeDistanceToTheGround();

            // 外部力リセット
            _externalForce = Vector2.zero;

            FrameExit();
            _worldSpeed = Speed;
        }

        /// <summary>
        /// フレーム初期化処理（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void FrameInitialization()
        {
            _contactList.Clear();

            // SIMD演算で新しい位置を計算
            float2 speedFloat2 = new float2(_speed.x, _speed.y);
            _newPosition = speedFloat2 * DeltaTime;

            // 状態の高速コピー（基底クラスのStateを使用）
            State.WasGroundedLastFrame = State.IsCollidingBelow;
            StandingOnLastFrame = StandingOn;
            State.WasTouchingTheCeilingLastFrame = State.IsCollidingAbove;
            CurrentWallCollider = null;
            _shouldComputeNewSpeed = true;
            State.Reset();
        }

        /// <summary>
        /// 重力適用処理（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void ApplyGravity()
        {
            _currentGravity = Parameters.Gravity;

            // 条件分岐を最小化
            float gravityMultiplier = 1f;
            if ( _speed.y > 0 )
            {
                gravityMultiplier = 1f / Parameters.AscentMultiplier;
            }
            else if ( _speed.y < 0 )
            {
                gravityMultiplier = Parameters.FallMultiplier;
            }

            _currentGravity *= gravityMultiplier;

            if ( _gravityActive )
            {
                float fallSlowFactor = math.select(1f, _fallSlowFactor, _fallSlowFactor != 0);
                _speed.y += (_currentGravity + _movingPlatformCurrentGravity) * fallSlowFactor * DeltaTime;
            }
        }

        /// <summary>
        /// レイパラメーター設定（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetRaysParameters()
        {
            // ボックスコライダーの境界計算をSIMDで最適化
            float2 offset = new float2(_boxCollider.offset.x, _boxCollider.offset.y);
            float2 size = new float2(_boxCollider.size.x, _boxCollider.size.y);

            // Transform行列を構築
            float4x4 transformMatrix = float4x4.TRS(
                transform.position,
                transform.rotation,
                transform.localScale
            );

            // Burst対応メソッドで境界角を計算
            float2x4 corners = CalculateBoundsCornersBurst(size, offset, transformMatrix);

            _boundsTopLeftCorner = corners.c0;
            _boundsTopRightCorner = corners.c1;
            _boundsBottomLeftCorner = corners.c2;
            _boundsBottomRightCorner = corners.c3;

            _boundsCenter = _boxCollider.bounds.center;
            _boundsWidth = math.distance(_boundsBottomLeftCorner, _boundsBottomRightCorner);
            _boundsHeight = math.distance(_boundsBottomLeftCorner, _boundsTopLeftCorner);
        }

        /// <summary>
        /// 横方向レイキャスト（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void CastRaysToTheSides(float raysDirection)
        {
            // レイキャストキャッシュを使用
            _raycastCache.horizontalFromBottom = (_boundsBottomRightCorner + _boundsBottomLeftCorner) * 0.5f;
            _raycastCache.horizontalToTop = (_boundsTopLeftCorner + _boundsTopRightCorner) * 0.5f;

            float2 obstacleOffset = new float2(0, ObstacleHeightTolerance);
            _raycastCache.horizontalFromBottom += obstacleOffset;
            _raycastCache.horizontalToTop -= obstacleOffset;


            // レイの長さを決定
            float horizontalRayLength = Mathf.Abs(_speed.x * DeltaTime) + _boundsWidth / 2 + RayOffsetHorizontal * 2 + RayExtraLengthHorizontal;

            // 必要に応じて保存領域のサイズを変更
            if ( _sideHitsStorage.Length != NumberOfHorizontalRays )
            {
                _sideHitsStorage = new RaycastHit2D[NumberOfHorizontalRays];
            }

            // 横方向にレイをキャスト
            for ( int i = 0; i < NumberOfHorizontalRays; i++ )
            {
                Vector2 rayOriginPoint = Vector2.Lerp(_raycastCache.horizontalFromBottom, _raycastCache.horizontalToTop, (float)i / (float)(NumberOfHorizontalRays - 1));

                // 前フレームで地面にいて、これが最初のレイの場合、ワンウェイプラットフォームに対してキャストしない
                if ( State.WasGroundedLastFrame && i == 0 )
                {
                    _sideHitsStorage[i] = MMDebug.RayCast(rayOriginPoint, raysDirection * (transform.right), horizontalRayLength, PlatformMask, MMColors.Indigo, Parameters.DrawRaycastsGizmos);
                }
                else
                {
                    _sideHitsStorage[i] = MMDebug.RayCast(rayOriginPoint, raysDirection * (transform.right), horizontalRayLength, PlatformMask & ~OneWayPlatformMask & ~MovingOneWayPlatformMask, MMColors.Indigo, Parameters.DrawRaycastsGizmos);
                }

                // 何かにヒットした場合
                if ( _sideHitsStorage[i].distance > 0 )
                {
                    // このコライダーが無視リストにある場合、ブレーク
                    if ( _sideHitsStorage[i].collider == _ignoredCollider )
                    {
                        break;
                    }

                    // 現在の横の斜面角度を決定し保存
                    float hitAngle = Mathf.Abs(Vector2.Angle(_sideHitsStorage[i].normal, transform.up));

                    if ( OneWayPlatformMask.MMContains(_sideHitsStorage[i].collider.gameObject) )
                    {
                        if ( hitAngle > 90 )
                        {
                            break;
                        }
                    }

                    // これが移動方向かどうかをチェック
                    if ( _movementDirection == raysDirection )
                    {
                        State.LateralSlopeAngle = hitAngle;
                    }

                    // 横の斜面角度が最大斜面角度より高い場合、壁にヒットしたことになり、それに応じてx移動を停止
                    if ( hitAngle > Parameters.MaximumSlopeAngle )
                    {
                        if ( raysDirection < 0 )
                        {
                            State.IsCollidingLeft = true;
                            State.DistanceToLeftCollider = _sideHitsStorage[i].distance;
                        }
                        else
                        {
                            State.IsCollidingRight = true;
                            State.DistanceToRightCollider = _sideHitsStorage[i].distance;
                        }

                        if ( (_movementDirection == raysDirection) || (CastRaysOnBothSides && (_speed.x == 0f)) )
                        {
                            CurrentWallCollider = _sideHitsStorage[i].collider.gameObject;
                            State.SlopeAngleOK = false;

                            float distance = MMMaths.DistanceBetweenPointAndLine(_sideHitsStorage[i].point, (Vector2)_raycastCache.horizontalFromBottom, (Vector2)_raycastCache.horizontalToTop);
                            if ( raysDirection <= 0 )
                            {
                                _newPosition.x = -distance
                                                 + _boundsWidth / 2
                                                 + RayOffsetHorizontal * 2;
                            }
                            else
                            {
                                _newPosition.x = distance
                                                 - _boundsWidth / 2
                                                 - RayOffsetHorizontal * 2;
                            }

                            // 空中にいる場合、キャラクターが押し戻されるのを防ぐ
                            if ( !State.IsGrounded && (Speed.y != 0) && (!Mathf.Approximately(hitAngle, 90f)) )
                            {
                                _newPosition.x = 0;
                            }

                            _contactList.Add(_sideHitsStorage[i]);
                            _speed.x = 0;
                            _shouldComputeNewSpeed = true;
                        }

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 下方向レイキャスト（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void CastRaysBelow()
        {
            _friction = 0;

            // 早期リターンの最適化
            bool isFalling = _newPosition.y < -_smallValue;
            State.IsFalling = isFalling;

            if ( Parameters.Gravity > 0 && !isFalling )
            {
                State.IsCollidingBelow = false;
                return;
            }

            // レイ長計算（Burst最適化）
            float rayLength = CalculateOptimizedRayLength(
                _boundsHeight,
                RayOffsetVertical,
                RayExtraLengthVertical,
                OnMovingPlatformRaycastLengthMultiplier,
                State.OnAMovingPlatform,
                _newPosition.y
            );

            // レイキャスト起点計算（Burst最適化）
            float2 verticalFromLeft, verticalFromRight;
            CalculateVerticalRaycastOrigins(
                _boundsBottomLeftCorner,
                _boundsTopLeftCorner,
                _boundsBottomRightCorner,
                _boundsTopRightCorner,
                new float2(_newPosition.x, RayOffsetVertical),
                out verticalFromLeft,
                out verticalFromRight
            );

            // キャッシュに保存
            _raycastCache.verticalFromLeft = verticalFromLeft;
            _raycastCache.verticalToRight = verticalFromRight;

            // レイヤーマスク設定
            LayerMask raysBelowMask = DetermineRaysBelowLayerMask();

            // レイキャスト実行とヒット処理
            float smallestDistance = float.MaxValue;
            int smallestDistanceIndex = 0;
            bool hitConnected = false;
            StandingOn = null;

            // 配列初期化
            for ( int i = 0; i < NumberOfVerticalRays; i++ )
            {
                StandingOnArray[i] = null;
            }

            // レイキャスト実行
            for ( int i = 0; i < NumberOfVerticalRays; i++ )
            {
                Vector2 rayOriginPoint = Vector2.Lerp(verticalFromLeft, verticalFromRight, (float)i / (float)(NumberOfVerticalRays - 1));

                if ( (_newPosition.y > 0) && (!State.WasGroundedLastFrame) )
                {
                    _optimizedBelowHitsStorage[i] = MMDebug.RayCast(rayOriginPoint, -transform.up, rayLength,
                        raysBelowMask & ~OneWayPlatformMask & ~MovingOneWayPlatformMask,
                        Color.blue, Parameters.DrawRaycastsGizmos);
                }
                else
                {
                    _optimizedBelowHitsStorage[i] = MMDebug.RayCast(rayOriginPoint, -transform.up, rayLength,
                        raysBelowMask, Color.blue, Parameters.DrawRaycastsGizmos);
                }

                if ( _optimizedBelowHitsStorage[i] )
                {
                    if ( _optimizedBelowHitsStorage[i].collider == _ignoredCollider )
                    {
                        continue;
                    }

                    hitConnected = true;

                    // 角度計算（Burst最適化）
                    float2 rayNormal = new float2(_optimizedBelowHitsStorage[i].normal.x, _optimizedBelowHitsStorage[i].normal.y);
                    float slopeAngle = CalculateSlopeAngle(rayNormal, new float2(transform.up.x, transform.up.y));

                    State.BelowSlopeAngle = slopeAngle;
                    State.BlowSlopeNormal = _optimizedBelowHitsStorage[i].normal;
                    State.BelowSlopeAngleAbsolute = MMMaths.AngleBetween(_optimizedBelowHitsStorage[i].normal, Vector2.up);

                    StandingOnArray[i] = _optimizedBelowHitsStorage[i].collider.gameObject;

                    if ( _optimizedBelowHitsStorage[i].distance < smallestDistance )
                    {
                        smallestDistanceIndex = i;
                        smallestDistance = _optimizedBelowHitsStorage[i].distance;
                    }
                }

                // 距離が小さすぎる場合は終了
                if ( smallestDistanceIndex < _optimizedBelowHitsStorage.Length && _optimizedBelowHitsStorage[smallestDistanceIndex] )
                {
                    float distance = MMMaths.DistanceBetweenPointAndLine(
                        _optimizedBelowHitsStorage[smallestDistanceIndex].point, (Vector2)verticalFromLeft, (Vector2)verticalFromRight);
                    if ( distance < _smallValue )
                    {
                        break;
                    }
                }
            }

            // ヒット処理
            if ( hitConnected )
            {
                ProcessBelowRaycastHit(smallestDistanceIndex, smallestDistance);
            }
            else
            {
                State.IsCollidingBelow = false;
                if ( State.OnAMovingPlatform )
                {
                    DetachFromMovingPlatform();
                }
            }

            // 斜面への張り付き処理
            if ( StickToSlopes )
            {
                StickToSlope();
            }
        }

        #region Burst最適化ヘルパーメソッド

        /// <summary>
        /// 垂直レイキャストの起点計算（Burst対応）
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculateVerticalRaycastOrigins(
            float2 bottomLeft, float2 topLeft, float2 bottomRight, float2 topRight,
            float2 offset, out float2 fromLeft, out float2 fromRight)
        {
            fromLeft = (bottomLeft + topLeft) * 0.5f + offset;
            fromRight = (bottomRight + topRight) * 0.5f + offset;
        }

        /// <summary>
        /// 斜面角度計算（Burst対応）
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateSlopeAngle(float2 normal, float2 up)
        {
            float angle = math.degrees(math.acos(math.dot(normal, up)));
            float3 cross = math.cross(new float3(up, 0), new float3(normal, 0));
            return cross.z < 0 ? -angle : angle;
        }

        /// <summary>
        /// 安全ボックスキャストサイズ計算（Burst対応）
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 CalculateSafetyBoxcastSize(float2 bounds, float2 ratio, float2 offset)
        {
            return bounds * ratio - offset;
        }

        /// <summary>
        /// レイキャスト起点X座標選択（Burst対応）
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SelectRaycastOriginX(float rightX, float leftX, bool useLeft)
        {
            return math.select(rightX, leftX, useLeft);
        }

        #endregion

        #region 下方向レイキャストヘルパーメソッド

        /// <summary>
        /// 下方向レイキャスト用のレイヤーマスク決定
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LayerMask DetermineRaysBelowLayerMask()
        {
            LayerMask raysBelowMask = PlatformMask;

            // 前フレームで立っていたオブジェクトの処理
            if ( StandingOnLastFrame != null )
            {
                _savedBelowLayer = StandingOnLastFrame.layer;
                if ( MidHeightOneWayPlatformMask.MMContains(StandingOnLastFrame.layer) )
                {
                    StandingOnLastFrame.layer = LayerMask.NameToLayer("Platforms");
                }
            }

            // ワンウェイプラットフォームの処理
            if ( State.WasGroundedLastFrame && StandingOnLastFrame != null )
            {
                if ( !MidHeightOneWayPlatformMask.MMContains(StandingOnLastFrame.layer) )
                {
                    raysBelowMask = PlatformMask & ~MidHeightOneWayPlatformMask;
                }
            }

            // 階段の処理
            if ( State.WasGroundedLastFrame && StandingOnLastFrame != null )
            {
                if ( StairsMask.MMContains(StandingOnLastFrame.layer) )
                {
                    if ( StandingOnCollider != null && StandingOnCollider.bounds.Contains(_colliderBottomCenterPosition) )
                    {
                        raysBelowMask = (raysBelowMask & ~OneWayPlatformMask) | StairsMask;
                    }
                }
            }

            // 移動プラットフォーム上で上昇中の処理
            if ( State.OnAMovingPlatform && (_newPosition.y > 0) )
            {
                raysBelowMask = raysBelowMask & ~OneWayPlatformMask;
            }

            return raysBelowMask;
        }

        /// <summary>
        /// 下方向レイキャストヒット処理
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessBelowRaycastHit(int hitIndex, float hitDistance)
        {
            if ( _optimizedBelowHitsStorage.Length <= hitIndex )
            {
                return;
            }

            var hit = _optimizedBelowHitsStorage[hitIndex];
            StandingOn = hit.collider.gameObject;
            StandingOnCollider = hit.collider;

            // ワンウェイプラットフォームへのジャンプ時の処理
            if ( !State.WasGroundedLastFrame
                && (hitDistance < _boundsHeight / 2)
                && (OneWayPlatformMask.MMContains(StandingOn.layer)
                    || (MovingOneWayPlatformMask.MMContains(StandingOn.layer) && (_speed.y > 0))) )
            {
                StandingOn = null;
                StandingOnCollider = null;
                State.IsCollidingBelow = false;
                return;
            }

            LastStandingOn = StandingOn;
            State.IsFalling = false;
            State.IsCollidingBelow = true;

            // 外部力の処理
            if ( _externalForce.y > 0 && _speed.y > 0 )
            {
                _newPosition.y = _speed.y * DeltaTime;
                State.IsCollidingBelow = false;
            }
            else
            {
                // Burst最適化された距離計算を使用
                float distance = CalculateDistancePointToLine(
                    new float2(hit.point.x, hit.point.y),
                    _raycastCache.verticalFromLeft,
                    _raycastCache.verticalToRight
                );

                _newPosition.y = -distance + _boundsHeight / 2 + RayOffsetVertical;
            }

            // 速度調整
            if ( !State.WasGroundedLastFrame && _speed.y > 0 )
            {
                if ( State.OnAMovingPlatform )
                {
                    _newPosition.y += _speed.y * DeltaTime;
                }
                else
                {
                    _newPosition.y = _speed.y * DeltaTime;
                    State.IsCollidingBelow = false;
                }
            }

            if ( Mathf.Abs(_newPosition.y) < _smallValue )
            {
                _newPosition.y = 0;
            }

            // 摩擦処理
            _frictionTest = hit.collider.gameObject.MMGetComponentNoAlloc<SurfaceModifier>();
            if ( (_frictionTest != null) && (_frictionTest.enabled) )
            {
                _friction = hit.collider.GetComponent<SurfaceModifier>().Friction;
            }

            // 移動プラットフォーム処理
            _movingPlatformTest = hit.collider.gameObject.MMGetComponentNoAlloc<MMPathMovement>();
            if ( _movingPlatformTest != null && State.IsGrounded )
            {
                _movingPlatform = _movingPlatformTest.GetComponent<MMPathMovement>();
            }
            else
            {
                DetachFromMovingPlatform();
            }
        }

        #endregion

        /// <summary>
        /// 新速度計算（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void ComputeNewSpeed()
        {
            if ( DeltaTime > 0 && _shouldComputeNewSpeed )
            {
                // SIMD演算で速度計算
                float2 newSpeed = new float2(_newPosition.x, _newPosition.y) / DeltaTime;
                _speed = new Vector2(newSpeed.x, newSpeed.y);
            }

            // 斜面速度係数の適用
            if ( State.IsGrounded )
            {
                float slopeFactor = Parameters.SlopeAngleSpeedFactor.Evaluate(
                    math.abs(State.BelowSlopeAngle) * math.sign(_speed.y)
                );
                _speed.x *= slopeFactor;
            }

            if ( !State.OnAMovingPlatform )
            {
                ClampSpeed();
                ClampExternalForce();
            }
        }

        /// <summary>
        /// 速度制限（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void ClampSpeed()
        {
            float2 speedFloat2 = new float2(_speed.x, _speed.y);
            float2 maxVelocity = new float2(Parameters.MaxVelocity.x, Parameters.MaxVelocity.y);

            speedFloat2 = math.clamp(speedFloat2, -maxVelocity, maxVelocity);
            _speed = new Vector2(speedFloat2.x, speedFloat2.y);
        }

        /// <summary>
        /// 外部力制限（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void ClampExternalForce()
        {
            float2 forceFloat2 = new float2(_externalForce.x, _externalForce.y);
            float2 maxVelocity = new float2(Parameters.MaxVelocity.x, Parameters.MaxVelocity.y);

            forceFloat2 = math.clamp(forceFloat2, -maxVelocity, maxVelocity);
            _externalForce = new Vector2(forceFloat2.x, forceFloat2.y);
        }

        /// <summary>
        /// 地面までの距離計算（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void ComputeDistanceToTheGround()
        {
            if ( DistanceToTheGroundRayMaximumLength <= 0 )
                return;

            if ( State.IsGrounded )
            {
                _distanceToTheGround = 0f;
                return;
            }

            // 条件分岐をmath.selectで最適化
            float xPosition = math.select(_boundsBottomRightCorner.x, _boundsBottomLeftCorner.x, State.BelowSlopeAngle < 0);
            _rayCastOrigin = new Vector2(xPosition, _boundsCenter.y);

            _distanceToTheGroundRaycast = MMDebug.RayCast(_rayCastOrigin, -transform.up, DistanceToTheGroundRayMaximumLength, _raysBelowLayerMaskPlatforms, MMColors.CadetBlue, true);

            if ( _distanceToTheGroundRaycast )
            {
                if ( _distanceToTheGroundRaycast.collider == _ignoredCollider )
                {
                    _distanceToTheGround = -1f;
                    return;
                }
                _distanceToTheGround = _distanceToTheGroundRaycast.distance - _boundsHeight / 2;
            }
            else
            {
                _distanceToTheGround = -1f;
            }
        }

        /// <summary>
        /// 力の追加（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddForce(Vector2 force)
        {
            // SIMD演算で力を追加
            float2 currentSpeed = new float2(_speed.x, _speed.y);
            float2 currentForce = new float2(_externalForce.x, _externalForce.y);
            float2 forceToAdd = new float2(force.x, force.y);

            currentSpeed += forceToAdd;
            currentForce += forceToAdd;

            _speed = new Vector2(currentSpeed.x, currentSpeed.y);
            _externalForce = new Vector2(currentForce.x, currentForce.y);

            ClampSpeed();
            ClampExternalForce();
        }

        /// <summary>
        /// 水平力の追加（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddHorizontalForce(float x)
        {
            _speed.x += x;
            _externalForce.x += x;
            ClampSpeed();
            ClampExternalForce();
        }

        /// <summary>
        /// 垂直力の追加（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddVerticalForce(float y)
        {
            _speed.y += y;
            _externalForce.y += y;
            ClampSpeed();
            ClampExternalForce();
        }

        /// <summary>
        /// 力の設定（最適化版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetForce(Vector2 force)
        {
            _speed = force;
            _externalForce = force;
            ClampSpeed();
            ClampExternalForce();
        }

        /// <summary>
        /// 境界計算を高速化するBurst対応メソッド
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2x4 CalculateBoundsCornersBurst(
            float2 boxColliderSize,
            float2 boxColliderOffset,
            float4x4 transformMatrix)
        {
            float2 halfSize = boxColliderSize * 0.5f;

            return new float2x4(
                math.transform(transformMatrix, new float3(boxColliderOffset + new float2(-halfSize.x, halfSize.y), 0)).xy,
                math.transform(transformMatrix, new float3(boxColliderOffset + new float2(halfSize.x, halfSize.y), 0)).xy,
                math.transform(transformMatrix, new float3(boxColliderOffset + new float2(-halfSize.x, -halfSize.y), 0)).xy,
                math.transform(transformMatrix, new float3(boxColliderOffset + new float2(halfSize.x, -halfSize.y), 0)).xy);
        }

        /// <summary>
        /// ベクトル正規化（Burst対応）
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 NormalizeVector(float2 vector)
        {
            return math.normalizesafe(vector);
        }

        /// <summary>
        /// 角度計算（Burst対応）
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateAngle(float2 from, float2 to)
        {
            return math.degrees(math.acos(math.dot(math.normalize(from), math.normalize(to))));
        }

        /// <summary>
        /// レイ長計算（Burst対応）
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateOptimizedRayLength(
            float boundsHeight,
            float rayOffsetVertical,
            float rayExtraLengthVertical,
            float onMovingPlatformMultiplier,
            bool isOnMovingPlatform,
            float newPositionY)
        {
            float baseLength = (boundsHeight * 0.5f) + rayOffsetVertical + rayExtraLengthVertical;

            // 条件付き乗算をmath.selectで最適化
            float platformMultiplier = math.select(1f, onMovingPlatformMultiplier, isOnMovingPlatform);
            baseLength *= platformMultiplier;

            // 負の移動時の追加長
            baseLength += math.select(0f, math.abs(newPositionY), newPositionY < 0);

            return baseLength;
        }

        /// <summary>
        /// 点と線の距離計算（Burst対応）
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateDistancePointToLine(float2 point, float2 lineStart, float2 lineEnd)
        {
            float2 line = lineEnd - lineStart;
            float2 pointVector = point - lineStart;

            float lineLengthSquared = math.lengthsq(line);
            if ( lineLengthSquared == 0 )
                return math.length(pointVector);

            float t = math.clamp(math.dot(pointVector, line) / lineLengthSquared, 0f, 1f);
            float2 projection = lineStart + t * line;

            return math.length(point - projection);
        }
    }
}