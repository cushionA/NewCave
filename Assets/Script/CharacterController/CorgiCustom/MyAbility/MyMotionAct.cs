using UnityEngine;
using System.Collections;
using Unity.Mathematics;
using MoreMountains.CorgiEngine;
using static ActionData;
using CharacterController;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using MyTool.Collection;

/// <summary>
/// モーション再生時に設定に従って上下左右に移動するコンポーネント
/// 地形接触で停止し、敵との接触はMoveContactTypeの設定に従う
/// 
/// ロック時はターゲットの位置に向かって移動
/// 非ロック時は最初に決めた方向に動く
/// 
/// 押す処理について変更
/// 押す処理は押される側が押す判定の攻撃を受けた時、通常サイズでかつ壁と接触していなかったら
/// 押す対象に入れられる。で、押す対象がいる間はそれを全部押し続ける
/// 攻撃を受けた側が攻撃を受けるたびに押されるかどうかを判定して、そうでなければそのリストから抜ける
/// 押す状態自体は別の攻撃を食らったり二秒経過したら解除される
/// 押す処理の場合、
/// </summary>
public class MyMotionAct : MyAbilityBase
{

    #region インスペクター設定

    [Header("接触判定設定")]
    [Tooltip("地形判定に使用するレイヤーマスク")]
    public LayerMask terrainLayerMask = -1;

    [Tooltip("敵判定に使用するレイヤーマスク")]
    public LayerMask enemyLayerMask = -1;

    [Tooltip("接触判定の範囲")]
    public Vector2 contactCheckSize = new Vector2(1f, 2f);

    [Header("キャラクター設定")]
    [Tooltip("キャラクターが右向きかどうか")]
    public bool isFacingRight = true;

    #endregion

    #region 内部変数

    // ActionDataの参照
    protected ActionData _actionData;

    // 移動状態管理
    protected RushState _currentRushState = RushState.停止;

    // 移動関連
    protected Vector2 _moveDirection;
    protected Vector2 _initialPosition;
    protected float _moveTimer;
    protected float _startMoveTimer;
    protected bool _isMoving = false;
    protected Vector2 _frameVelocity;

    // 累計移動距離管理
    protected Vector2 _totalMovedDistance = Vector2.zero;
    protected Vector2 _remainingMoveDistance = Vector2.zero;

    /// <summary>
    /// ロックオン対象のハッシュ
    /// </summary>
    protected float2 _lockOnTargetPos;

    /// <summary>
    /// ロックオン行動かつターゲットがいる場合にtrue
    /// </summary>
    protected bool _isLock;

    /// <summary>
    /// 押す相手のコーギーエンジンの参照を持つ
    /// 自分では操作しない
    /// 押される相手が被弾時に判定・操作する
    /// </summary>
    private NonAllocationList<(MyCorgiController, float)> _pushList;

    /// <summary>
    /// 速度調整用アニメーションカーブ
    /// アクションデータの設定から静的データを取得する
    /// </summary>
    private AnimationCurve _speedCurve;

    #endregion

    #region 公開メソッド

    protected override void Initialization()
    {
        base.Initialization();

        _pushList = new NonAllocationList<MyCorgiController>(5);
    }

    /// <summary>
    /// ActionDataを設定してアビリティを開始
    /// </summary>
    /// <param name="actionData">移動データ</param>
    public virtual void StartMove(ActionData actionData)
    {
        if ( _currentRushState != RushState.停止 )
            return;

        // ActionDataを保存
        _actionData = actionData;

        // ロック行動でターゲットいるならロック設定
        if ( _actionData.lockAction && _character.TargetHash != -1 )
        {
            _isLock = true;
            _lockOnTargetPos = AIManager.instance.characterDataDictionary.GetPosition(_character.TargetHash);
        }

        // 初期化
        _initialPosition = _character.Position;
        _moveTimer = 0f;
        _totalMovedDistance = Vector2.zero;
        _remainingMoveDistance = new Vector2(
            Mathf.Abs(_actionData.moveDistance.x),
            Mathf.Abs(_actionData.moveDistance.y)
        );

        // 最初から現時点の時間に待機時間を乗せた値で待機を行う。
        _startMoveTimer = _controller.DeltaTime + actionData.startMoveTime;
        _pushList.Clear();

        // 移動方向を決定
        DetermineMoveDirection();

        // 待機状態に移行
        _currentRushState = RushState.待機;
    }

    /// <summary>
    /// アビリティの停止処理
    /// </summary>
    public virtual void EndMove()
    {
        _currentRushState = RushState.停止;
        _isMoving = false;
        _pushList.Clear();
        _actionData = null;
        _totalMovedDistance = Vector2.zero;
        _remainingMoveDistance = Vector2.zero;

        // CorgiControllerの速度をリセット
        _controller.SetForce(Vector2.zero);
    }

    /// <summary>
    /// ロックオン対象を設定
    /// </summary>
    /// <param name="target">ロックオン対象</param>
    public virtual void SetLockOnTarget(int target)
    {
        _lockOnTargetPos = target;
    }

    /// <summary>
    /// 現在の移動状態を取得
    /// </summary>
    public RushState CurrentState => _currentRushState;

    /// <summary>
    /// 移動中かどうか
    /// </summary>
    public bool IsMoving => _isMoving;

    #endregion

    #region Unity ライフサイクル

    public override void ProcessAbility()
    {
        if ( _actionData == null )
            return;

        switch ( _currentRushState )
        {
            case RushState.待機:
                ProcessWaiting();
                break;

            case RushState.移動:
                ProcessMoving();
                break;
        }
    }

    #endregion

    #region 状態処理

    /// <summary>
    /// 待機状態の処理
    /// </summary>
    protected virtual void ProcessWaiting()
    {
        if ( _controller.DeltaTime > _startMoveTimer )
        {
            _currentRushState = RushState.移動;
            _moveTimer = _actionData.moveDuration + _controller.DeltaTime;
            _isMoving = true;
        }
    }

    /// <summary>
    /// 移動状態の処理
    /// </summary>
    protected virtual void ProcessMoving()
    {

        // 移動時間終了チェック
        if ( _controller.DeltaTime > _moveTimer )
        {
            EndMove();
            return;
        }

        // 累計移動距離による停止チェック
        if ( _remainingMoveDistance.x <= 0f && _remainingMoveDistance.y <= 0f )
        {
            EndMove();
            return;
        }

        // ロック時は毎フレーム移動方向を更新
        if ( _isLock )
        {
            DetermineMoveDirection();
        }

        // 移動実行
        Vector3 frameMovement = CalculateFrameMovement();

        // 地形接触チェック（常に優先）
        if ( CheckTerrainContact(frameMovement) )
        {
            _controller.SetForce(Vector2.zero);
            return;
        }

        // 敵接触チェックと処理
        if ( CheckEnemyContact(frameMovement) )
        {
            _controller.SetForce(Vector2.zero);

        }

        // 移動実行
        ExecuteMovement(frameMovement);
    }

    #endregion

    #region 移動計算

    /// <summary>
    /// 移動方向を決定
    /// </summary>
    protected virtual void DetermineMoveDirection()
    {
        if ( _isLock )
        {
            // ロックオン時の移動方向計算
            float2 currentPos = _character.Position;
            float2 targetPos = AIManager.instance.characterDataDictionary.GetPosition(_character.TargetHash);
            float2 directionToTarget = targetPos - currentPos;

            float moveX = 0f;
            float moveY = 0f;

            // X方向の移動距離が残っていて、ターゲットがX方向にいる場合
            if ( _remainingMoveDistance.x > 0f )
            {
                if ( directionToTarget.x > 0f )      // ターゲットが右にいる
                    moveX = _actionData.moveDistance.x > 0 ? 1f : -1f;  // 元の移動方向の符号を保持
                else if ( directionToTarget.x < 0f ) // ターゲットが左にいる
                    moveX = _actionData.moveDistance.x > 0 ? -1f : 1f; // 元の移動方向と逆
            }

            // Y方向の移動距離が残っていて、ターゲットがY方向にいる場合
            if ( _remainingMoveDistance.y > 0f )
            {
                if ( directionToTarget.y > 0f )      // ターゲットが上にいる
                    moveY = _actionData.moveDistance.y > 0 ? 1f : -1f;  // 元の移動方向の符号を保持
                else if ( directionToTarget.y < 0f ) // ターゲットが下にいる
                    moveY = _actionData.moveDistance.y > 0 ? -1f : 1f; // 元の移動方向と逆
            }

            _moveDirection.Set(moveX, moveY);
        }
        else
        {
            // 通常の移動方向（キャラクターの向きを取得）
            bool facingRight = _character != null ? _character.IsFacingRight : isFacingRight;
            float directionX = facingRight ? 1f : -1f;
            if ( _actionData.moveDistance.x < 0 )
                directionX *= -1f; // 元の設定が負の場合は逆方向

            float directionY = _actionData.moveDistance.y > 0 ? 1f : -1f;

            _moveDirection.Set(directionX, directionY);
        }
    }

    /// <summary>
    /// フレーム単位の移動量を計算
    /// </summary>
    protected virtual Vector3 CalculateFrameMovement()
    {
        // 時間の進行度を計算（0.0 ～ 1.0）
        float timeProgress = (_controller.DeltaTime - (_moveTimer - _actionData.moveDuration)) / _actionData.moveDuration;
        timeProgress = Mathf.Clamp01(timeProgress);

        // アニメーションカーブから速度倍率を取得
        float speedMultiplier = 1f;
        if ( _speedCurve != null )
        {
            speedMultiplier = _speedCurve.Evaluate(timeProgress);
        }

        // 基本移動速度を計算
        float baseSpeedX = _remainingMoveDistance.x / _actionData.moveDuration;
        float baseSpeedY = _remainingMoveDistance.y / _actionData.moveDuration;

        // フレーム移動量を計算
        Vector2 frameMovement = new Vector2(
            _moveDirection.x * baseSpeedX * speedMultiplier * _controller.DeltaTime,
            _moveDirection.y * baseSpeedY * speedMultiplier * _controller.DeltaTime
        );

        // 残り移動距離を超えないように制限
        if ( Mathf.Abs(frameMovement.x) > _remainingMoveDistance.x )
        {
            frameMovement.x = _moveDirection.x * _remainingMoveDistance.x;
        }
        if ( Mathf.Abs(frameMovement.y) > _remainingMoveDistance.y )
        {
            frameMovement.y = _moveDirection.y * _remainingMoveDistance.y;
        }

        // フレーム速度として保存（CorgiControllerで使用）
        _frameVelocity = frameMovement / _controller.DeltaTime;

        return frameMovement;
    }

    /// <summary>
    /// 移動を実行（CorgiControllerを使用）
    /// </summary>
    protected virtual void ExecuteMovement(Vector3 movement)
    {
        // 累計移動距離を更新
        Vector2 movementMagnitude = new Vector2(Mathf.Abs(movement.x), Mathf.Abs(movement.y));
        _totalMovedDistance += movementMagnitude;
        _remainingMoveDistance -= movementMagnitude;

        // 残り移動距離が0以下になったら0にクランプ
        _remainingMoveDistance = Vector2.Max(_remainingMoveDistance, Vector2.zero);

        // 現在の速度を設定
        _controller.SetForce(_frameVelocity);

        // 敵を押している場合は敵も移動
        if ( _pushList.Any() && _actionData.contactType == MoveContactType.押す )
        {
            var pushSpan = _pushList.AsSpan();

            for ( int i = 0; i < pushSpan.Length; i++ )
            {
                // 敵もCorgiControllerで移動
                pushSpan[i].SetForce(_frameVelocity);
            }

        }
    }

    #endregion

    #region 接触判定

    /// <summary>
    /// 地形との接触をチェック（CorgiControllerの当たり判定を使用）
    /// </summary>
    protected virtual bool CheckTerrainContact(Vector3 movement)
    {
        // フォールバック：OverlapBoxを使用
        Vector3 checkPosition = transform.position + movement;
        Collider2D terrainHit = Physics2D.OverlapBox(
            checkPosition,
            contactCheckSize,
            0f,
            terrainLayerMask
        );

        return terrainHit != null;
    }

    /// <summary>
    /// 敵との接触をチェック
    /// </summary>
    protected virtual bool CheckEnemyContact(Vector2 movement)
    {
        if ( _actionData.contactType != MoveContactType.停止 )
            return false;

        Vector2 checkPosition = (Vector2)_character.Position + movement;
        Collider2D enemyHit = Physics2D.OverlapBox(
            checkPosition,
            contactCheckSize,
            0f,
            enemyLayerMask
        );

        if ( enemyHit != null && enemyHit.gameObject != gameObject )
        {
            return true;
        }

        return false;
    }

    #endregion

    #region 押し処理判定（実装予定）

    /// <summary>
    /// 攻撃ヒット時のイベント
    /// </summary>
    /// <param name="hitCharacter"></param>
    public override void OnAttack(MyCharacter hitCharacter)
    {
        _pushList.TryAdd((hitCharacter.Controller, _controller.DeltaTime));
    }

    #endregion

    #region デバッグ描画

    /// <summary>
    /// 接触判定範囲をギズモで描画
    /// </summary>
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, contactCheckSize);

        if ( _isMoving && Application.isPlaying )
        {
            Gizmos.color = Color.red;
            Vector3 currentDirection = new Vector3(_moveDirection.x, _moveDirection.y, 0f);
            Vector3 targetPos = transform.position + currentDirection * _remainingMoveDistance.magnitude;
            Gizmos.DrawLine(transform.position, targetPos);

            // 残り移動距離を表示
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }

    #endregion
}