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
/// ���[�V�����Đ����ɐݒ�ɏ]���ď㉺���E�Ɉړ�����R���|�[�l���g
/// �n�`�ڐG�Œ�~���A�G�Ƃ̐ڐG��MoveContactType�̐ݒ�ɏ]��
/// 
/// ���b�N���̓^�[�Q�b�g�̈ʒu�Ɍ������Ĉړ�
/// �񃍃b�N���͍ŏ��Ɍ��߂������ɓ���
/// 
/// ���������ɂ��ĕύX
/// ���������͉�����鑤����������̍U�����󂯂����A�ʏ�T�C�Y�ł��ǂƐڐG���Ă��Ȃ�������
/// �����Ώۂɓ������B�ŁA�����Ώۂ�����Ԃ͂����S������������
/// �U�����󂯂������U�����󂯂邽�тɉ�����邩�ǂ����𔻒肵�āA�����łȂ���΂��̃��X�g���甲����
/// ������Ԏ��͕̂ʂ̍U����H��������b�o�߂�������������
/// ���������̏ꍇ�A
/// </summary>
public class MyMotionAct : MyAbilityBase
{

    #region �C���X�y�N�^�[�ݒ�

    [Header("�ڐG����ݒ�")]
    [Tooltip("�n�`����Ɏg�p���郌�C���[�}�X�N")]
    public LayerMask terrainLayerMask = -1;

    [Tooltip("�G����Ɏg�p���郌�C���[�}�X�N")]
    public LayerMask enemyLayerMask = -1;

    [Tooltip("�ڐG����͈̔�")]
    public Vector2 contactCheckSize = new Vector2(1f, 2f);

    [Header("�L�����N�^�[�ݒ�")]
    [Tooltip("�L�����N�^�[���E�������ǂ���")]
    public bool isFacingRight = true;

    #endregion

    #region �����ϐ�

    // ActionData�̎Q��
    protected ActionData _actionData;

    // �ړ���ԊǗ�
    protected RushState _currentRushState = RushState.��~;

    // �ړ��֘A
    protected Vector2 _moveDirection;
    protected Vector2 _initialPosition;
    protected float _moveTimer;
    protected float _startMoveTimer;
    protected bool _isMoving = false;
    protected Vector2 _frameVelocity;

    // �݌v�ړ������Ǘ�
    protected Vector2 _totalMovedDistance = Vector2.zero;
    protected Vector2 _remainingMoveDistance = Vector2.zero;

    /// <summary>
    /// ���b�N�I���Ώۂ̃n�b�V��
    /// </summary>
    protected float2 _lockOnTargetPos;

    /// <summary>
    /// ���b�N�I���s�����^�[�Q�b�g������ꍇ��true
    /// </summary>
    protected bool _isLock;

    /// <summary>
    /// ��������̃R�[�M�[�G���W���̎Q�Ƃ�����
    /// �����ł͑��삵�Ȃ�
    /// ������鑊�肪��e���ɔ���E���삷��
    /// </summary>
    private NonAllocationList<(MyCorgiController, float)> _pushList;

    /// <summary>
    /// ���x�����p�A�j���[�V�����J�[�u
    /// �A�N�V�����f�[�^�̐ݒ肩��ÓI�f�[�^���擾����
    /// </summary>
    private AnimationCurve _speedCurve;

    #endregion

    #region ���J���\�b�h

    protected override void Initialization()
    {
        base.Initialization();

        _pushList = new NonAllocationList<MyCorgiController>(5);
    }

    /// <summary>
    /// ActionData��ݒ肵�ăA�r���e�B���J�n
    /// </summary>
    /// <param name="actionData">�ړ��f�[�^</param>
    public virtual void StartMove(ActionData actionData)
    {
        if ( _currentRushState != RushState.��~ )
            return;

        // ActionData��ۑ�
        _actionData = actionData;

        // ���b�N�s���Ń^�[�Q�b�g����Ȃ烍�b�N�ݒ�
        if ( _actionData.lockAction && _character.TargetHash != -1 )
        {
            _isLock = true;
            _lockOnTargetPos = AIManager.instance.characterDataDictionary.GetPosition(_character.TargetHash);
        }

        // ������
        _initialPosition = _character.Position;
        _moveTimer = 0f;
        _totalMovedDistance = Vector2.zero;
        _remainingMoveDistance = new Vector2(
            Mathf.Abs(_actionData.moveDistance.x),
            Mathf.Abs(_actionData.moveDistance.y)
        );

        // �ŏ����猻���_�̎��Ԃɑҋ@���Ԃ��悹���l�őҋ@���s���B
        _startMoveTimer = _controller.DeltaTime + actionData.startMoveTime;
        _pushList.Clear();

        // �ړ�����������
        DetermineMoveDirection();

        // �ҋ@��ԂɈڍs
        _currentRushState = RushState.�ҋ@;
    }

    /// <summary>
    /// �A�r���e�B�̒�~����
    /// </summary>
    public virtual void EndMove()
    {
        _currentRushState = RushState.��~;
        _isMoving = false;
        _pushList.Clear();
        _actionData = null;
        _totalMovedDistance = Vector2.zero;
        _remainingMoveDistance = Vector2.zero;

        // CorgiController�̑��x�����Z�b�g
        _controller.SetForce(Vector2.zero);
    }

    /// <summary>
    /// ���b�N�I���Ώۂ�ݒ�
    /// </summary>
    /// <param name="target">���b�N�I���Ώ�</param>
    public virtual void SetLockOnTarget(int target)
    {
        _lockOnTargetPos = target;
    }

    /// <summary>
    /// ���݂̈ړ���Ԃ��擾
    /// </summary>
    public RushState CurrentState => _currentRushState;

    /// <summary>
    /// �ړ������ǂ���
    /// </summary>
    public bool IsMoving => _isMoving;

    #endregion

    #region Unity ���C�t�T�C�N��

    public override void ProcessAbility()
    {
        if ( _actionData == null )
            return;

        switch ( _currentRushState )
        {
            case RushState.�ҋ@:
                ProcessWaiting();
                break;

            case RushState.�ړ�:
                ProcessMoving();
                break;
        }
    }

    #endregion

    #region ��ԏ���

    /// <summary>
    /// �ҋ@��Ԃ̏���
    /// </summary>
    protected virtual void ProcessWaiting()
    {
        if ( _controller.DeltaTime > _startMoveTimer )
        {
            _currentRushState = RushState.�ړ�;
            _moveTimer = _actionData.moveDuration + _controller.DeltaTime;
            _isMoving = true;
        }
    }

    /// <summary>
    /// �ړ���Ԃ̏���
    /// </summary>
    protected virtual void ProcessMoving()
    {

        // �ړ����ԏI���`�F�b�N
        if ( _controller.DeltaTime > _moveTimer )
        {
            EndMove();
            return;
        }

        // �݌v�ړ������ɂ���~�`�F�b�N
        if ( _remainingMoveDistance.x <= 0f && _remainingMoveDistance.y <= 0f )
        {
            EndMove();
            return;
        }

        // ���b�N���͖��t���[���ړ��������X�V
        if ( _isLock )
        {
            DetermineMoveDirection();
        }

        // �ړ����s
        Vector3 frameMovement = CalculateFrameMovement();

        // �n�`�ڐG�`�F�b�N�i��ɗD��j
        if ( CheckTerrainContact(frameMovement) )
        {
            _controller.SetForce(Vector2.zero);
            return;
        }

        // �G�ڐG�`�F�b�N�Ə���
        if ( CheckEnemyContact(frameMovement) )
        {
            _controller.SetForce(Vector2.zero);

        }

        // �ړ����s
        ExecuteMovement(frameMovement);
    }

    #endregion

    #region �ړ��v�Z

    /// <summary>
    /// �ړ�����������
    /// </summary>
    protected virtual void DetermineMoveDirection()
    {
        if ( _isLock )
        {
            // ���b�N�I�����̈ړ������v�Z
            float2 currentPos = _character.Position;
            float2 targetPos = AIManager.instance.characterDataDictionary.GetPosition(_character.TargetHash);
            float2 directionToTarget = targetPos - currentPos;

            float moveX = 0f;
            float moveY = 0f;

            // X�����̈ړ��������c���Ă��āA�^�[�Q�b�g��X�����ɂ���ꍇ
            if ( _remainingMoveDistance.x > 0f )
            {
                if ( directionToTarget.x > 0f )      // �^�[�Q�b�g���E�ɂ���
                    moveX = _actionData.moveDistance.x > 0 ? 1f : -1f;  // ���̈ړ������̕�����ێ�
                else if ( directionToTarget.x < 0f ) // �^�[�Q�b�g�����ɂ���
                    moveX = _actionData.moveDistance.x > 0 ? -1f : 1f; // ���̈ړ������Ƌt
            }

            // Y�����̈ړ��������c���Ă��āA�^�[�Q�b�g��Y�����ɂ���ꍇ
            if ( _remainingMoveDistance.y > 0f )
            {
                if ( directionToTarget.y > 0f )      // �^�[�Q�b�g����ɂ���
                    moveY = _actionData.moveDistance.y > 0 ? 1f : -1f;  // ���̈ړ������̕�����ێ�
                else if ( directionToTarget.y < 0f ) // �^�[�Q�b�g�����ɂ���
                    moveY = _actionData.moveDistance.y > 0 ? -1f : 1f; // ���̈ړ������Ƌt
            }

            _moveDirection.Set(moveX, moveY);
        }
        else
        {
            // �ʏ�̈ړ������i�L�����N�^�[�̌������擾�j
            bool facingRight = _character != null ? _character.IsFacingRight : isFacingRight;
            float directionX = facingRight ? 1f : -1f;
            if ( _actionData.moveDistance.x < 0 )
                directionX *= -1f; // ���̐ݒ肪���̏ꍇ�͋t����

            float directionY = _actionData.moveDistance.y > 0 ? 1f : -1f;

            _moveDirection.Set(directionX, directionY);
        }
    }

    /// <summary>
    /// �t���[���P�ʂ̈ړ��ʂ��v�Z
    /// </summary>
    protected virtual Vector3 CalculateFrameMovement()
    {
        // ���Ԃ̐i�s�x���v�Z�i0.0 �` 1.0�j
        float timeProgress = (_controller.DeltaTime - (_moveTimer - _actionData.moveDuration)) / _actionData.moveDuration;
        timeProgress = Mathf.Clamp01(timeProgress);

        // �A�j���[�V�����J�[�u���瑬�x�{�����擾
        float speedMultiplier = 1f;
        if ( _speedCurve != null )
        {
            speedMultiplier = _speedCurve.Evaluate(timeProgress);
        }

        // ��{�ړ����x���v�Z
        float baseSpeedX = _remainingMoveDistance.x / _actionData.moveDuration;
        float baseSpeedY = _remainingMoveDistance.y / _actionData.moveDuration;

        // �t���[���ړ��ʂ��v�Z
        Vector2 frameMovement = new Vector2(
            _moveDirection.x * baseSpeedX * speedMultiplier * _controller.DeltaTime,
            _moveDirection.y * baseSpeedY * speedMultiplier * _controller.DeltaTime
        );

        // �c��ړ������𒴂��Ȃ��悤�ɐ���
        if ( Mathf.Abs(frameMovement.x) > _remainingMoveDistance.x )
        {
            frameMovement.x = _moveDirection.x * _remainingMoveDistance.x;
        }
        if ( Mathf.Abs(frameMovement.y) > _remainingMoveDistance.y )
        {
            frameMovement.y = _moveDirection.y * _remainingMoveDistance.y;
        }

        // �t���[�����x�Ƃ��ĕۑ��iCorgiController�Ŏg�p�j
        _frameVelocity = frameMovement / _controller.DeltaTime;

        return frameMovement;
    }

    /// <summary>
    /// �ړ������s�iCorgiController���g�p�j
    /// </summary>
    protected virtual void ExecuteMovement(Vector3 movement)
    {
        // �݌v�ړ��������X�V
        Vector2 movementMagnitude = new Vector2(Mathf.Abs(movement.x), Mathf.Abs(movement.y));
        _totalMovedDistance += movementMagnitude;
        _remainingMoveDistance -= movementMagnitude;

        // �c��ړ�������0�ȉ��ɂȂ�����0�ɃN�����v
        _remainingMoveDistance = Vector2.Max(_remainingMoveDistance, Vector2.zero);

        // ���݂̑��x��ݒ�
        _controller.SetForce(_frameVelocity);

        // �G�������Ă���ꍇ�͓G���ړ�
        if ( _pushList.Any() && _actionData.contactType == MoveContactType.���� )
        {
            var pushSpan = _pushList.AsSpan();

            for ( int i = 0; i < pushSpan.Length; i++ )
            {
                // �G��CorgiController�ňړ�
                pushSpan[i].SetForce(_frameVelocity);
            }

        }
    }

    #endregion

    #region �ڐG����

    /// <summary>
    /// �n�`�Ƃ̐ڐG���`�F�b�N�iCorgiController�̓����蔻����g�p�j
    /// </summary>
    protected virtual bool CheckTerrainContact(Vector3 movement)
    {
        // �t�H�[���o�b�N�FOverlapBox���g�p
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
    /// �G�Ƃ̐ڐG���`�F�b�N
    /// </summary>
    protected virtual bool CheckEnemyContact(Vector2 movement)
    {
        if ( _actionData.contactType != MoveContactType.��~ )
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

    #region ������������i�����\��j

    /// <summary>
    /// �U���q�b�g���̃C�x���g
    /// </summary>
    /// <param name="hitCharacter"></param>
    public override void OnAttack(MyCharacter hitCharacter)
    {
        _pushList.TryAdd((hitCharacter.Controller, _controller.DeltaTime));
    }

    #endregion

    #region �f�o�b�O�`��

    /// <summary>
    /// �ڐG����͈͂��M�Y���ŕ`��
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

            // �c��ړ�������\��
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }

    #endregion
}