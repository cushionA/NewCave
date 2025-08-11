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
    /// �œK�����ꂽCorgiController����
    /// Burst Compile�ASIMD���߁Astackalloc�����p���č�����
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Core/My Corgi Controller")]
    public unsafe class MyCorgiController : CorgiController
    {
        // �L���b�V���p�\���́iSIMD�œK���j
        private struct RaycastCache
        {
            public float2 horizontalFromBottom;
            public float2 horizontalToTop;
            public float2 verticalFromLeft;
            public float2 verticalToRight;
            public float2 aboveStart;
            public float2 aboveEnd;
        }

        // Burst�Ή��̌v�Z�p�\���́i���l�f�[�^�̂݁j
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

        // ���C�L���X�g���ʕۑ��p�z��
        private RaycastHit2D[] _optimizedBelowHitsStorage;

        /// <summary>
        /// ���t���[�����s����鏈���i�œK���Łj
        /// </summary>
        protected override void EveryFrame()
        {
            // �^�C���X�P�[���`�F�b�N��������
            if ( Time.timeScale == 0f )
                return;

            // �d�͓K�p�iSIMD�œK���j
            ApplyGravity();

            // �t���[��������
            FrameInitialization();

            // ���C�L���X�g����
            SetRaysParameters();

            // �ړ��v���b�g�t�H�[������
            HandleMovingPlatforms(_movingPlatform);
            HandleMovingPlatforms(_pusherPlatform, true);

            ForcesApplied = _speed;

            // ���C�L���X�g���s
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

            // Transform�ړ�
            MoveTransform();

            // �㏈��
            SetRaysParameters();
            ComputeNewSpeed();
            SetStates();
            ComputeDistanceToTheGround();

            // �O���̓��Z�b�g
            _externalForce = Vector2.zero;

            FrameExit();
            _worldSpeed = Speed;
        }

        /// <summary>
        /// �t���[�������������i�œK���Łj
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void FrameInitialization()
        {
            _contactList.Clear();

            // SIMD���Z�ŐV�����ʒu���v�Z
            float2 speedFloat2 = new float2(_speed.x, _speed.y);
            _newPosition = speedFloat2 * DeltaTime;

            // ��Ԃ̍����R�s�[�i���N���X��State���g�p�j
            State.WasGroundedLastFrame = State.IsCollidingBelow;
            StandingOnLastFrame = StandingOn;
            State.WasTouchingTheCeilingLastFrame = State.IsCollidingAbove;
            CurrentWallCollider = null;
            _shouldComputeNewSpeed = true;
            State.Reset();
        }

        /// <summary>
        /// �d�͓K�p�����i�œK���Łj
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void ApplyGravity()
        {
            _currentGravity = Parameters.Gravity;

            // ����������ŏ���
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
        /// ���C�p�����[�^�[�ݒ�i�œK���Łj
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetRaysParameters()
        {
            // �{�b�N�X�R���C�_�[�̋��E�v�Z��SIMD�ōœK��
            float2 offset = new float2(_boxCollider.offset.x, _boxCollider.offset.y);
            float2 size = new float2(_boxCollider.size.x, _boxCollider.size.y);

            // Transform�s����\�z
            float4x4 transformMatrix = float4x4.TRS(
                transform.position,
                transform.rotation,
                transform.localScale
            );

            // Burst�Ή����\�b�h�ŋ��E�p���v�Z
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
        /// ���������C�L���X�g�i�œK���Łj
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void CastRaysToTheSides(float raysDirection)
        {
            // ���C�L���X�g�L���b�V�����g�p
            _raycastCache.horizontalFromBottom = (_boundsBottomRightCorner + _boundsBottomLeftCorner) * 0.5f;
            _raycastCache.horizontalToTop = (_boundsTopLeftCorner + _boundsTopRightCorner) * 0.5f;

            float2 obstacleOffset = new float2(0, ObstacleHeightTolerance);
            _raycastCache.horizontalFromBottom += obstacleOffset;
            _raycastCache.horizontalToTop -= obstacleOffset;


            // ���C�̒���������
            float horizontalRayLength = Mathf.Abs(_speed.x * DeltaTime) + _boundsWidth / 2 + RayOffsetHorizontal * 2 + RayExtraLengthHorizontal;

            // �K�v�ɉ����ĕۑ��̈�̃T�C�Y��ύX
            if ( _sideHitsStorage.Length != NumberOfHorizontalRays )
            {
                _sideHitsStorage = new RaycastHit2D[NumberOfHorizontalRays];
            }

            // �������Ƀ��C���L���X�g
            for ( int i = 0; i < NumberOfHorizontalRays; i++ )
            {
                Vector2 rayOriginPoint = Vector2.Lerp(_raycastCache.horizontalFromBottom, _raycastCache.horizontalToTop, (float)i / (float)(NumberOfHorizontalRays - 1));

                // �O�t���[���Œn�ʂɂ��āA���ꂪ�ŏ��̃��C�̏ꍇ�A�����E�F�C�v���b�g�t�H�[���ɑ΂��ăL���X�g���Ȃ�
                if ( State.WasGroundedLastFrame && i == 0 )
                {
                    _sideHitsStorage[i] = MMDebug.RayCast(rayOriginPoint, raysDirection * (transform.right), horizontalRayLength, PlatformMask, MMColors.Indigo, Parameters.DrawRaycastsGizmos);
                }
                else
                {
                    _sideHitsStorage[i] = MMDebug.RayCast(rayOriginPoint, raysDirection * (transform.right), horizontalRayLength, PlatformMask & ~OneWayPlatformMask & ~MovingOneWayPlatformMask, MMColors.Indigo, Parameters.DrawRaycastsGizmos);
                }

                // �����Ƀq�b�g�����ꍇ
                if ( _sideHitsStorage[i].distance > 0 )
                {
                    // ���̃R���C�_�[���������X�g�ɂ���ꍇ�A�u���[�N
                    if ( _sideHitsStorage[i].collider == _ignoredCollider )
                    {
                        break;
                    }

                    // ���݂̉��̎Ζʊp�x�����肵�ۑ�
                    float hitAngle = Mathf.Abs(Vector2.Angle(_sideHitsStorage[i].normal, transform.up));

                    if ( OneWayPlatformMask.MMContains(_sideHitsStorage[i].collider.gameObject) )
                    {
                        if ( hitAngle > 90 )
                        {
                            break;
                        }
                    }

                    // ���ꂪ�ړ��������ǂ������`�F�b�N
                    if ( _movementDirection == raysDirection )
                    {
                        State.LateralSlopeAngle = hitAngle;
                    }

                    // ���̎Ζʊp�x���ő�Ζʊp�x��荂���ꍇ�A�ǂɃq�b�g�������ƂɂȂ�A����ɉ�����x�ړ����~
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

                            // �󒆂ɂ���ꍇ�A�L�����N�^�[�������߂����̂�h��
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
        /// ���������C�L���X�g�i�œK���Łj
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void CastRaysBelow()
        {
            _friction = 0;

            // �������^�[���̍œK��
            bool isFalling = _newPosition.y < -_smallValue;
            State.IsFalling = isFalling;

            if ( Parameters.Gravity > 0 && !isFalling )
            {
                State.IsCollidingBelow = false;
                return;
            }

            // ���C���v�Z�iBurst�œK���j
            float rayLength = CalculateOptimizedRayLength(
                _boundsHeight,
                RayOffsetVertical,
                RayExtraLengthVertical,
                OnMovingPlatformRaycastLengthMultiplier,
                State.OnAMovingPlatform,
                _newPosition.y
            );

            // ���C�L���X�g�N�_�v�Z�iBurst�œK���j
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

            // �L���b�V���ɕۑ�
            _raycastCache.verticalFromLeft = verticalFromLeft;
            _raycastCache.verticalToRight = verticalFromRight;

            // ���C���[�}�X�N�ݒ�
            LayerMask raysBelowMask = DetermineRaysBelowLayerMask();

            // ���C�L���X�g���s�ƃq�b�g����
            float smallestDistance = float.MaxValue;
            int smallestDistanceIndex = 0;
            bool hitConnected = false;
            StandingOn = null;

            // �z�񏉊���
            for ( int i = 0; i < NumberOfVerticalRays; i++ )
            {
                StandingOnArray[i] = null;
            }

            // ���C�L���X�g���s
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

                    // �p�x�v�Z�iBurst�œK���j
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

                // ����������������ꍇ�͏I��
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

            // �q�b�g����
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

            // �Ζʂւ̒���t������
            if ( StickToSlopes )
            {
                StickToSlope();
            }
        }

        #region Burst�œK���w���p�[���\�b�h

        /// <summary>
        /// �������C�L���X�g�̋N�_�v�Z�iBurst�Ή��j
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
        /// �Ζʊp�x�v�Z�iBurst�Ή��j
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
        /// ���S�{�b�N�X�L���X�g�T�C�Y�v�Z�iBurst�Ή��j
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 CalculateSafetyBoxcastSize(float2 bounds, float2 ratio, float2 offset)
        {
            return bounds * ratio - offset;
        }

        /// <summary>
        /// ���C�L���X�g�N�_X���W�I���iBurst�Ή��j
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SelectRaycastOriginX(float rightX, float leftX, bool useLeft)
        {
            return math.select(rightX, leftX, useLeft);
        }

        #endregion

        #region ���������C�L���X�g�w���p�[���\�b�h

        /// <summary>
        /// ���������C�L���X�g�p�̃��C���[�}�X�N����
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LayerMask DetermineRaysBelowLayerMask()
        {
            LayerMask raysBelowMask = PlatformMask;

            // �O�t���[���ŗ����Ă����I�u�W�F�N�g�̏���
            if ( StandingOnLastFrame != null )
            {
                _savedBelowLayer = StandingOnLastFrame.layer;
                if ( MidHeightOneWayPlatformMask.MMContains(StandingOnLastFrame.layer) )
                {
                    StandingOnLastFrame.layer = LayerMask.NameToLayer("Platforms");
                }
            }

            // �����E�F�C�v���b�g�t�H�[���̏���
            if ( State.WasGroundedLastFrame && StandingOnLastFrame != null )
            {
                if ( !MidHeightOneWayPlatformMask.MMContains(StandingOnLastFrame.layer) )
                {
                    raysBelowMask = PlatformMask & ~MidHeightOneWayPlatformMask;
                }
            }

            // �K�i�̏���
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

            // �ړ��v���b�g�t�H�[����ŏ㏸���̏���
            if ( State.OnAMovingPlatform && (_newPosition.y > 0) )
            {
                raysBelowMask = raysBelowMask & ~OneWayPlatformMask;
            }

            return raysBelowMask;
        }

        /// <summary>
        /// ���������C�L���X�g�q�b�g����
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

            // �����E�F�C�v���b�g�t�H�[���ւ̃W�����v���̏���
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

            // �O���͂̏���
            if ( _externalForce.y > 0 && _speed.y > 0 )
            {
                _newPosition.y = _speed.y * DeltaTime;
                State.IsCollidingBelow = false;
            }
            else
            {
                // Burst�œK�����ꂽ�����v�Z���g�p
                float distance = CalculateDistancePointToLine(
                    new float2(hit.point.x, hit.point.y),
                    _raycastCache.verticalFromLeft,
                    _raycastCache.verticalToRight
                );

                _newPosition.y = -distance + _boundsHeight / 2 + RayOffsetVertical;
            }

            // ���x����
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

            // ���C����
            _frictionTest = hit.collider.gameObject.MMGetComponentNoAlloc<SurfaceModifier>();
            if ( (_frictionTest != null) && (_frictionTest.enabled) )
            {
                _friction = hit.collider.GetComponent<SurfaceModifier>().Friction;
            }

            // �ړ��v���b�g�t�H�[������
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
        /// �V���x�v�Z�i�œK���Łj
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void ComputeNewSpeed()
        {
            if ( DeltaTime > 0 && _shouldComputeNewSpeed )
            {
                // SIMD���Z�ő��x�v�Z
                float2 newSpeed = new float2(_newPosition.x, _newPosition.y) / DeltaTime;
                _speed = new Vector2(newSpeed.x, newSpeed.y);
            }

            // �Ζʑ��x�W���̓K�p
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
        /// ���x�����i�œK���Łj
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
        /// �O���͐����i�œK���Łj
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
        /// �n�ʂ܂ł̋����v�Z�i�œK���Łj
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

            // ���������math.select�ōœK��
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
        /// �͂̒ǉ��i�œK���Łj
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void AddForce(Vector2 force)
        {
            // SIMD���Z�ŗ͂�ǉ�
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
        /// �����͂̒ǉ��i�œK���Łj
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
        /// �����͂̒ǉ��i�œK���Łj
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
        /// �͂̐ݒ�i�œK���Łj
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
        /// ���E�v�Z������������Burst�Ή����\�b�h
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
        /// �x�N�g�����K���iBurst�Ή��j
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 NormalizeVector(float2 vector)
        {
            return math.normalizesafe(vector);
        }

        /// <summary>
        /// �p�x�v�Z�iBurst�Ή��j
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateAngle(float2 from, float2 to)
        {
            return math.degrees(math.acos(math.dot(math.normalize(from), math.normalize(to))));
        }

        /// <summary>
        /// ���C���v�Z�iBurst�Ή��j
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

            // �����t����Z��math.select�ōœK��
            float platformMultiplier = math.select(1f, onMovingPlatformMultiplier, isOnMovingPlatform);
            baseLength *= platformMultiplier;

            // ���̈ړ����̒ǉ���
            baseLength += math.select(0f, math.abs(newPositionY), newPositionY < 0);

            return baseLength;
        }

        /// <summary>
        /// �_�Ɛ��̋����v�Z�iBurst�Ή��j
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