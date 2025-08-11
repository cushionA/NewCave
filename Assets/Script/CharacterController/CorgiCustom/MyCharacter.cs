using CharacterController;
using CharacterController.StatusData;
using Cysharp.Threading.Tasks;
using MoreMountains.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.StatusData.BrainStatus;

namespace MoreMountains.CorgiEngine
{
    [SelectionBase]
    /// <summary>
    /// ���̃N���X�̓L�����N�^�[��CorgiController�R���|�[�l���g�𑀏c���܂��B
    /// �����ɃL�����N�^�[�̃Q�[�����[���i�W�����v�A�_�b�V���A�ˌ��Ȃǁj�����ׂĎ������܂��B
    /// �A�j���[�^�[�p�����[�^�[: Grounded (bool), xSpeed (float), ySpeed (float), 
    /// CollidingLeft (bool), CollidingRight (bool), CollidingBelow (bool), CollidingAbove (bool), Idle (bool)
    /// Random : ���t���[���X�V�����0�`1�̃����_���l�B��ԑJ�ڂɃo���G�[�V������ǉ�����̂ɕ֗�
    /// RandomConstant : Start���ɐ��������0�`1000�̃����_��int�l�B���̃A�j���[�^�[�̐������Ԓ��͒萔�Ƃ��ĕێ������B
    /// �����^�C�v�̃L�����N�^�[���قȂ�s�������悤�ɂ���̂ɕ֗�
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Core/MyCharacter")]
    public class MyCharacter : Character
    {
        #region ��`

        #region enum��`

        /// <summary>
        /// ���f���ʂ��܂Ƃ߂Ċi�[����r�b�g���Z�p
        /// </summary>
        [Flags]
        public enum JudgeResult : byte
        {
            �����Ȃ� = 0,
            ���[�h�ύX���� = 1 << 1,// ���̎��͈ړ��������ς���
            �^�[�Q�b�g�ύX���� = 1 << 2,
            �s����ύX���� = 1 << 3,
            ������ύX���� = 1 << 4,
        }

        #endregion enum��`

        #region �\���̒�`

        /// <summary>
        /// �s���Ɏg�p����f�[�^�̍\���́B
        /// ���݂̍s����ԁA�ړ������A���f��A�ȂǕK�v�Ȃ��̂͑S�Ď��߂�B
        /// ����ɏ]���ē����Ƃ����f�[�^�B
        /// 28Byte
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct MovementInfo
        {

            // ����L�������ǂ��������A�݂����ȃf�[�^�܂œ���Ă�
            // �V�K���f��̓^�[�Q�b�g����ւ��Ƃ��A���f���ԓ���ւ��Ƃ�������ƃL�����f�[�^��������B

            /// <summary>
            /// �^�[�Q�b�g�̃n�b�V���R�[�h
            /// ����ő�����擾����B
            /// �����ւ̎x�����[�u�̃^�[�Q�b�g�����肤�邱�Ƃ͓��ɓ����B
            /// </summary>
            public int targetHash;

            /// <summary>
            /// ���݂̃^�[�Q�b�g�Ƃ̋����B�}�C�i�X������̂ŕ����ł�����B
            /// </summary>
            public float targetDistance;

            /// <summary>
            /// �ԍ��ōs�����w�肷��B
            /// �U���Ɍ��炸�����Ƃ����S���B�ړ���������g�p���[�V�����A�s���̎�ʂ܂Łi���@�Ƃ��ړ��Ƃ��j
            /// �������̍\���f�[�^�̓X�e�[�^�X�Ɏ������Ƃ��B�s����Ԃ��Ƃɔԍ��Ŏw�肳�ꂽ�s��������B
            /// ��ԕύX�̏ꍇ�A����ŕύX��̏�Ԃ��w�肷��B
            /// </summary>
            public byte actNum;

            /// <summary>
            /// �ύX��̃��[�h�B
            /// </summary>
            public byte changeMode;

            /// <summary>
            /// ���f���ʂɂ��Ă̏����i�[����r�b�g
            /// </summary>
            public JudgeResult result;

            /// <summary>
            /// �f�o�b�O�p�B
            /// �I�������s��������ݒ肷��B
            /// </summary>
            public byte selectActCondition;

            /// <summary>
            /// �f�o�b�O�p�B
            /// �I�������^�[�Q�b�g�I��������ݒ肷��B
            /// </summary>
            public byte selectTargetCondition;

            /// <summary>
            /// �V�K���f���̏���
            /// �s���I����H
            /// </summary>
            public void JudgeUpdate(int hashCode)
            {
                // ���f�����L�����f�[�^�ɔ��f����B
                // ���ԂɊւ��Ă̓Q�[���}�l�[�W���[������Ƀ}�l�[�W���[����Ƃ�悤�ɕύX�����B
                AIManager.instance.characterDataDictionary.UpdateDataAfterJudge(hashCode, actNum, result, 0);
            }

            /// <summary>
            /// 
            /// </summary>
            public string GetDebugData()
            {
                return $"{this.selectActCondition}�Ԗڂ̏����A{(TargetSelectCondition)this.selectTargetCondition}({this.selectTargetCondition})�Ŕ��f";
            }

        }

        #endregion

        #endregion ��`

        #region �t�B�[���h

        [Header("�w���X")]
        /// ���̃L�����N�^�[�Ɋ֘A�t����ꂽHealth�X�N���v�g�A��̏ꍇ�͎����I�Ɏ擾����܂�
        [Tooltip("���̃L�����N�^�[�Ɋ֘A�t����ꂽHealth�X�N���v�g�A��̏ꍇ�͎����I�Ɏ擾����܂�")]
        public new MyHealth CharacterHealth;

        /// <summary>
        /// �R���f�B�V������ԃ}�V��
        /// </summary>
        [HideInInspector]
        public new MyConditionStateMachine ConditionState;

        /// <summary>
        /// �ꎞ�I�ȃR���f�B�V�����ύX���������邽�߂̒�~�g�[�N��
        /// </summary>
        private CancellationTokenSource _conditionChangeCancellationTokenSource;

        protected new MyCorgiController _controller;

        /// �e�X�g�Ŏg�p����X�e�[�^�X�B<br></br>
        /// ���f�Ԋu�̃f�[�^�������Ă���B<br></br>
        /// �C���X�y�N�^����ݒ�B
        /// </summary>
        [SerializeField]
        protected BrainStatus status;

        /// <summary>
        /// ���Ȓ�`�A�r���e�B�̃L���b�V��
        /// </summary>
        protected new MyAbilityBase[] _characterAbilities;

        /// <summary>
        /// ���񔻒f�������𐔂���B<br></br>
        /// �񓯊��Ɠ����ŁA���҂��锻�f�񐔂Ƃ̊Ԃ̌덷���قȂ邩������B<br></br>
        /// �ŏ��̍s���̕�����1�����������l�ɁB
        /// </summary>
        [HideInInspector]
        public long judgeCount = -1;

        /// <summary>
        /// �Q�[���I�u�W�F�N�g�̃n�b�V���l
        /// </summary>
        [HideInInspector]
        public int myHash;

        #endregion �t�B�[���h

        #region �v���p�e�B


        /// <summary>
        /// �L�����N�^�[�̗l�X�ȏ��
        /// ���̂��Ȃ��s�v�Ȃ̂ŉB��
        /// </summary>
        private new CharacterStates CharacterState { get; set; }

        /// <summary>
        /// �����̈ʒu��Ԃ��v���p�e�B
        /// </summary>
        public float2 Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AIManager.instance.characterDataDictionary.GetPosition(myHash);
        }

        /// <summary>
        /// ���̃L�����̌��݂̃^�[�Q�b�g���擾����v���p�e�B
        /// </summary>
        public int TargetHash
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AIManager.instance.characterDataDictionary.GetTargetHash(myHash);
        }


        /// <summary>
        /// ���̃L�����̌��݂̃^�[�Q�b�g���擾����v���p�e�B
        /// </summary>
        public MyCorgiController Controller
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _controller;
        }

        #endregion �v���p�e�B

        #region �R�[�M�[�R���g���[���[���\�b�h

        /// <summary>
        /// ���̓}�l�[�W���[�A�J�����A�R���|�[�l���g���擾�E�ۑ����܂�
        /// </summary>
        public override void Initialization()
        {
            // �X�e�[�g�}�V����������
            MovementState = new MMStateMachine<CharacterStates.MovementStates>(this.gameObject, SendStateChangeEvents);
            ConditionState = new MyConditionStateMachine(this.gameObject, SendStateChangeEvents);

            MovementState.ChangeState(CharacterStates.MovementStates.Idle);

            if ( InitialFacingDirection == FacingDirections.Left )
            {
                IsFacingRight = false;
            }
            else
            {
                IsFacingRight = true;
            }

            // �J�����^�[�Q�b�g���C���X�^���X��
            if ( CameraTarget == null )
            {
                CameraTarget = new GameObject();
                CameraTarget.transform.SetParent(this.transform);
                CameraTarget.transform.localPosition = Vector3.zero;
                CameraTarget.name = "CameraTarget";
            }
            _cameraTargetInitialPosition = CameraTarget.transform.localPosition;

            // ���݂̓��̓}�l�[�W���[���擾
            SetInputManager();
            GetMainCamera();

            // �R���|�[�l���g�������̎g�p�̂��߂ɕۑ�
            _spriteRenderer = this.gameObject.GetComponent<SpriteRenderer>();
            _controller = this.gameObject.GetComponent<MyCorgiController>();
            _characterPersistence = this.gameObject.GetComponent<CharacterPersistence>();
            CacheAbilitiesAtInit();
            if ( CharacterBrain == null )
            {
                CharacterBrain = this.gameObject.GetComponent<AIBrain>();
            }
            if ( CharacterBrain != null )
            {
                CharacterBrain.Owner = this.gameObject;
            }
            if ( CharacterHealth == null )
            {
                CharacterHealth = this.gameObject.GetComponent<MyHealth>();
            }
            _damageOnTouch = this.gameObject.GetComponent<DamageOnTouch>();
            CanFlip = true;
            AssignAnimator();

            _originalGravity = _controller.Parameters.Gravity;

            _conditionChangeCancellationTokenSource = new CancellationTokenSource();

            ForceSpawnDirection();

            // �V�����L�����f�[�^�𑗂�A�R���o�b�g�}�l�[�W���[�ɑ���B
            // ����A����ς�ޗ������Č������ō���Ă��炨���B
            // NativeContainer�܂ލ\���̂��R�s�[����̂Ȃ񂩂��킢�B
            // ���������R�s�[���Ă��A�������ō�������̓��[�J���ϐ��ł����Ȃ�����Dispose()����̖��͂Ȃ��͂��B
            AIManager.instance.CharacterAdd(this.status, this);
        }

        /// <summary>
        /// ���t���[�����s���܂��B���_����������邽�߂�Update���番������Ă��܂��B
        /// </summary>
        protected override void EveryFrame()
        {
            HandleCharacterStatus();

            // �A�r���e�B������
            EarlyProcessAbilities();

            if ( Time.timeScale != 0f )
            {
                ProcessAbilities();
                LateProcessAbilities();

                // �J�����^�[�Q�b�g���X�V���鏈���͕s�v
                // proCamera 2d�g���̂�
                //HandleCameraTarget();
            }

            // �e���Ԃ��A�j���[�^�[�ɑ��M
            UpdateAnimators();
            RotateModel();
        }

        /// <summary>
        /// �A�r���e�B���擾���A�����̎g�p�̂��߂ɃL���b�V�����܂�
        /// ���s���ɃA�r���e�B��ǉ�����ꍇ�́A���̃��\�b�h��K���Ăяo���Ă�������
        /// ���z�I�ɂ́A���s���ɃR���|�[�l���g��ǉ����邱�Ƃ͔����������̂ł��B�R�X�g�������邩��ł��B
        /// ����ɃR���|�[�l���g��L����/���������邱�Ƃ������߂��܂��B
        /// �������A�K�v�ȏꍇ�́A���̃��\�b�h���Ăяo���Ă��������B
        /// </summary>
        public override void CacheAbilities()
        {
            // �����̃��x���ŃA�r���e�B�����ׂĎ擾
            _characterAbilities = this.gameObject.GetComponents<MyAbilityBase>();

            // ���[�U�[����葽���̃m�[�h���w�肵�Ă���ꍇ
            if ( (AdditionalAbilityNodes != null) && (AdditionalAbilityNodes.Count > 0) )
            {
                // �ꎞ���X�g���쐬
                List<MyAbilityBase> tempAbilityList = new List<MyAbilityBase>();

                // ���łɌ������A�r���e�B�����ׂă��X�g�ɓ����
                for ( int i = 0; i < _characterAbilities.Length; i++ )
                {
                    tempAbilityList.Add(_characterAbilities[i]);
                }

                // �m�[�h����̂��̂�ǉ�
                for ( int j = 0; j < AdditionalAbilityNodes.Count; j++ )
                {
                    MyAbilityBase[] tempArray = AdditionalAbilityNodes[j].GetComponentsInChildren<MyAbilityBase>();
                    foreach ( MyAbilityBase ability in tempArray )
                    {
                        tempAbilityList.Add(ability);
                    }
                }

                _characterAbilities = tempAbilityList.ToArray();
            }
            _abilitiesCachedOnce = true;
        }

        /// <summary>
        /// �o�^����Ă��邷�ׂẴA�r���e�B��Early Process���\�b�h���Ăяo���܂�
        /// </summary>
        protected override void EarlyProcessAbilities()
        {
            foreach ( MyAbilityBase ability in _characterAbilities )
            {
                if ( ability.enabled && ability.AbilityInitialized )
                {
                    ability.EarlyProcessAbility();
                }
            }
        }

        /// <summary>
        /// �o�^����Ă��邷�ׂẴA�r���e�B��Process���\�b�h���Ăяo���܂�
        /// </summary>
        protected override void ProcessAbilities()
        {
            foreach ( MyAbilityBase ability in _characterAbilities )
            {
                if ( ability.enabled && ability.AbilityInitialized )
                {
                    ability.ProcessAbility();
                }
            }
        }

        /// <summary>
        /// �o�^����Ă��邷�ׂẴA�r���e�B��Late Process���\�b�h���Ăяo���܂�
        /// </summary>
        protected override void LateProcessAbilities()
        {
            foreach ( MyAbilityBase ability in _characterAbilities )
            {
                if ( ability.enabled && ability.AbilityInitialized )
                {
                    ability.LateProcessAbility();
                }
            }
        }

        /// <summary>
        /// �A�j���[�^�[�p�����[�^�[�����������܂��B
        /// </summary>
        protected override void InitializeAnimatorParameters()
        {
            if ( _animator == null )
            { return; }

            _animatorParameters = new HashSet<int>();

            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _groundedAnimationParameterName, out _groundedAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _fallingAnimationParameterName, out _fallingAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _airborneAnimationParameterName, out _airborneSpeedAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _xSpeedAnimationParameterName, out _xSpeedAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _ySpeedAnimationParameterName, out _ySpeedAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _xSpeedAbsoluteAnimationParameterName, out _xSpeedAbsoluteAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _ySpeedAbsoluteAnimationParameterName, out _ySpeedAbsoluteAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _worldXSpeedAnimationParameterName, out _worldXSpeedAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _worldYSpeedAnimationParameterName, out _worldYSpeedAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _collidingLeftAnimationParameterName, out _collidingLeftAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _collidingRightAnimationParameterName, out _collidingRightAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _collidingBelowAnimationParameterName, out _collidingBelowAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _collidingAboveAnimationParameterName, out _collidingAboveAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _idleSpeedAnimationParameterName, out _idleSpeedAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _aliveAnimationParameterName, out _aliveAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _facingRightAnimationParameterName, out _facingRightAnimationParameter, AnimatorControllerParameterType.Bool, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _randomAnimationParameterName, out _randomAnimationParameter, AnimatorControllerParameterType.Float, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _randomConstantAnimationParameterName, out _randomConstantAnimationParameter, AnimatorControllerParameterType.Int, _animatorParameters);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(_animator, _flipAnimationParameterName, out _flipAnimationParameter, AnimatorControllerParameterType.Trigger, _animatorParameters);

            // �萔�t���[�g�A�j���[�V�����p�����[�^�[���X�V
            int randomConstant = UnityEngine.Random.Range(0, 1000);
            MMAnimatorExtensions.UpdateAnimatorInteger(_animator, _randomConstantAnimationParameter, randomConstant, _animatorParameters);
        }

        /// <summary>
        /// Update()�ŌĂяo����A�e�A�j���[�^�[�p�����[�^�[��Ή�����State�l�ɐݒ肵�܂�
        /// </summary>
        protected override void UpdateAnimators()
        {
            if ( (UseDefaultMecanim) && (_animator != null) )
            {
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _groundedAnimationParameter, _controller.State.IsGrounded, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _fallingAnimationParameter, MovementState.CurrentState == CharacterStates.MovementStates.Falling, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _airborneSpeedAnimationParameter, Airborne, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _aliveAnimationParameter, (ConditionState.CurrentState != CharacterStates.CharacterConditions.Dead), _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _xSpeedAnimationParameter, _controller.Speed.x, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _ySpeedAnimationParameter, _controller.Speed.y, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _xSpeedAbsoluteAnimationParameter, Mathf.Abs(_controller.Speed.x), _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _ySpeedAbsoluteAnimationParameter, Mathf.Abs(_controller.Speed.y), _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _worldXSpeedAnimationParameter, _controller.WorldSpeed.x, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _worldYSpeedAnimationParameter, _controller.WorldSpeed.y, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _collidingLeftAnimationParameter, _controller.State.IsCollidingLeft, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _collidingRightAnimationParameter, _controller.State.IsCollidingRight, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _collidingBelowAnimationParameter, _controller.State.IsCollidingBelow, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _collidingAboveAnimationParameter, _controller.State.IsCollidingAbove, _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _idleSpeedAnimationParameter, (MovementState.CurrentState == CharacterStates.MovementStates.Idle), _animatorParameters, PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _facingRightAnimationParameter, IsFacingRight, _animatorParameters);

                UpdateAnimationRandomNumber();
                MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _randomAnimationParameter, _animatorRandomNumber, _animatorParameters, PerformAnimatorSanityChecks);

                foreach ( MyAbilityBase ability in _characterAbilities )
                {
                    if ( ability.enabled && ability.AbilityInitialized )
                    {
                        ability.UpdateAnimator();
                    }
                }
            }
        }

        /// <summary>
        /// �L�����N�^�[�̏�Ԃ��������܂��B
        /// </summary>
        protected override void HandleCharacterStatus()
        {
            // �L�����N�^�[������ł���ꍇ�A�����ړ���h��
            if ( ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead )
            {
                if ( CharacterHealth != null )
                {
                    if ( CharacterHealth.GravityOffOnDeath )
                    {
                        _controller.GravityActive(false);
                    }
                    if ( CharacterHealth.ApplyDeathForce && (CharacterHealth.DeathForce.x == 0f) )
                    {
                        _controller.SetHorizontalForce(0);
                        return;
                    }
                }
                else
                {
                    _controller.SetHorizontalForce(0);
                    return;
                }
            }

            // �L�����N�^�[���������Ă���ꍇ�A�ړ���h��
            if ( ConditionState.CurrentState == CharacterStates.CharacterConditions.Frozen )
            {
                _controller.GravityActive(false);
                _controller.SetForce(Vector2.zero);
            }
        }

        /// <summary>
        /// ���̃L�����N�^�[�𓀌����܂��B
        /// </summary>
        public virtual void Freeze()
        {
            _controller.GravityActive(false);
            _controller.SetForce(Vector2.zero);
            if ( ConditionState.CurrentState != CharacterStates.CharacterConditions.Frozen )
            {
                _conditionStateBeforeFreeze = ConditionState.CurrentState;
            }
            ConditionState.ChangeState(CharacterStates.CharacterConditions.Frozen);
        }

        /// <summary>
        /// ���̃L�����N�^�[�̓������������܂�
        /// </summary>
        public virtual void UnFreeze()
        {
            _controller.GravityActive(true);
            ConditionState.ChangeState(_conditionStateBeforeFreeze);
        }

        /// <summary>
        /// �v���C���[�𖳌��ɂ��邽�߂ɌĂяo����܂��i�Ⴆ�΃��x���̏I���Ɂj�B
        /// ����ȍ~�A�ړ�����͂ւ̉����͂��܂���B
        /// </summary>
        public virtual void Disable()
        {
            enabled = false;
            _controller.enabled = false;
            this.gameObject.MMGetComponentNoAlloc<Collider2D>().enabled = false;
        }

        /// <summary>
        /// �p�����[�^�[�œn���ꂽ�ꏊ�Ńv���C���[�����X�|�[�������܂�
        /// </summary>
        /// <param name="spawnPoint">���X�|�[���̏ꏊ</param>
        public virtual void RespawnAt(Transform spawnPoint, FacingDirections facingDirection)
        {
            if ( !gameObject.activeInHierarchy )
            {
                //Debug.LogError("Spawn : your Character's gameobject is inactive");
                return;
            }

            UnFreeze();

            // �L�����N�^�[�������������������Ă��邱�Ƃ��m�F
            Face(facingDirection);

            // ������h�点��i����ł����ꍇ�j
            ConditionState.ChangeState(CharacterStates.CharacterConditions.Normal);
            // 2D�R���C�_�[���ėL����
            this.gameObject.MMGetComponentNoAlloc<Collider2D>().enabled = true;
            // �ĂуR���W����������������
            _controller.CollisionsOn();


            transform.position = spawnPoint.position;
            Physics2D.SyncTransforms();

            if ( CharacterHealth != null )
            {
                if ( _characterPersistence != null )
                {
                    if ( _characterPersistence.Initialized )
                    {
                        if ( CharacterHealth != null )
                        {
                            CharacterHealth.UpdateHealthBar(false);
                        }
                        return;
                    }
                }

                CharacterHealth.ResetHealthToMaxHealth();
                CharacterHealth.Revive();
            }
        }

        /// <summary>
        /// �L�����N�^�[�Ƃ��̈ˑ��֌W�i�W�F�b�g�p�b�N�Ȃǁj�𐅕��ɔ��]���܂�
        /// </summary>
        public virtual void Flip(bool IgnoreFlipOnDirectionChange = false)
        {
            // �L�����N�^�[�𔽓]���������Ȃ��ꍇ�́A���������ɏI��
            if ( !FlipModelOnDirectionChange && !RotateModelOnDirectionChange && !IgnoreFlipOnDirectionChange )
            {
                return;
            }

            if ( !CanFlip )
            {
                return;
            }

            if ( !FlipModelOnDirectionChange && !RotateModelOnDirectionChange && IgnoreFlipOnDirectionChange )
            {
                if ( CharacterModel != null )
                {
                    CharacterModel.transform.localScale = Vector3.Scale(CharacterModel.transform.localScale, ModelFlipValue);
                }
                else
                {
                    // �X�v���C�g�����_���[�x�[�X�̏ꍇ�AflipX�����𔽓]
                    if ( _spriteRenderer != null )
                    {
                        _spriteRenderer.flipX = !_spriteRenderer.flipX;
                    }
                }
            }

            // �L�����N�^�[�𐅕��ɔ��]
            FlipModel();

            if ( _animator != null )
            {
                MMAnimatorExtensions.SetAnimatorTrigger(_animator, _flipAnimationParameter, _animatorParameters, PerformAnimatorSanityChecks);
            }

            IsFacingRight = !IsFacingRight;

            // ���ׂẴA�r���e�B�ɔ��]���邱�Ƃ�`����
            foreach ( MyAbilityBase ability in _characterAbilities )
            {
                if ( ability.enabled )
                {
                    ability.Flip();
                }
            }
        }

        /// <summary>
        /// �w�肵�����ԃL�����N�^�[�̃R���f�B�V������ύX���A���̌ナ�Z�b�g���邽�߂Ɏg�p���܂��B
        /// ���΂炭�̊ԏd�͂𖳌��ɂ��A�I�v�V�����ŗ͂����Z�b�g�ł��܂��B
        /// </summary>
        /// <param name="newCondition"></param>
        /// <param name="duration"></param>
        /// <param name="resetControllerForces"></param>
        /// <param name="disableGravity"></param>
        public override void ChangeCharacterConditionTemporarily(CharacterStates.CharacterConditions newCondition,
            float duration, bool resetControllerForces, bool disableGravity)
        {
            if ( _conditionChangeCancellationTokenSource != null )
            {
                _conditionChangeCancellationTokenSource.Cancel();
                _conditionChangeCancellationTokenSource.Dispose();
                _conditionChangeCancellationTokenSource = new CancellationTokenSource();
            }

            ChangeCharacterConditionTemporarilyTask(newCondition, duration, resetControllerForces, disableGravity, _conditionChangeCancellationTokenSource).Forget();
        }

        /// <summary>
        /// ChangeCharacterConditionTemporarily�ɂ��ꎞ�I�ȃR���f�B�V�����ύX����������UnitaskVoid���\�b�h�B
        /// �L�����Z���g�[�N���g���Ă�̂ő��p���Ȃ��������������B
        /// </summary>
        /// <param name="newCondition"></param>
        /// <param name="duration"></param>
        /// <param name="resetControllerForces"></param>
        /// <param name="disableGravity"></param>
        /// <returns></returns>
        protected virtual async UniTaskVoid ChangeCharacterConditionTemporarilyTask(
            CharacterStates.CharacterConditions newCondition,
            float duration, bool resetControllerForces, bool disableGravity, CancellationTokenSource tokenSource)
        {
            if ( this.ConditionState.CurrentState != newCondition )
            {
                _lastState = this.ConditionState.CurrentState;
            }

            this.ConditionState.ChangeState(newCondition);
            if ( resetControllerForces )
            { _controller.SetForce(Vector2.zero); }

            if ( disableGravity && (_controller != null) )
            { _controller.GravityActive(false); }

            await UniTask.WaitForSeconds(duration, cancellationToken: tokenSource.Token);

            this.ConditionState.ChangeState(_lastState);
            if ( disableGravity && (_controller != null) )
            { _controller.GravityActive(true); }
        }

        #region �C�x���g

        /// <summary>
        /// �L�����N�^�[�����S�����Ƃ��ɌĂяo����܂��B
        /// ���ׂẴA�r���e�B��Reset()���\�b�h���Ăяo���̂ŁA�K�v�ɉ����Đݒ�����̒l�ɕ����ł��܂�
        /// </summary>
        public virtual void Reset()
        {
            _spawnDirectionForced = false;
            if ( _characterAbilities == null )
            {
                return;
            }
            if ( _characterAbilities.Length == 0 )
            {
                return;
            }
            foreach ( MyAbilityBase ability in _characterAbilities )
            {
                if ( ability.enabled )
                {
                    ability.ResetAbility();
                }
            }
        }

        /// <summary>
        /// �h�����ɁA�X�|�[���������������܂�
        /// </summary>
        protected virtual void OnRevive()
        {
            ForceSpawnDirection();
            if ( CharacterBrain != null )
            {
                CharacterBrain.enabled = true;
            }
            if ( _damageOnTouch != null )
            {
                _damageOnTouch.enabled = true;
            }
        }

        /// <summary>
        /// �L�����N�^�[���S���ɁA�u���C���ƃ_���[�W�I���^�b�`�G���A�𖳌��ɂ��܂�
        /// </summary>
        protected virtual void OnDeath()
        {
            if ( CharacterBrain != null )
            {
                CharacterBrain.TransitionToState("");
                CharacterBrain.enabled = false;
            }
            if ( _damageOnTouch != null )
            {
                _damageOnTouch.enabled = false;
            }
        }

        /// <summary>
        /// OnEnable���ɁAOnRevive�C�x���g��o�^���܂�
        /// </summary>
        protected virtual void OnEnable()
        {
            if ( CharacterHealth != null )
            {
                CharacterHealth.OnRevive += OnRevive;
                CharacterHealth.OnDeath += OnDeath;
            }
        }

        /// <summary>
        /// OnDisable���ɁAOnRevive�C�x���g�̓o�^���������܂�
        /// </summary>
        protected virtual void OnDisable()
        {
            if ( CharacterHealth != null )
            {
                //_health.OnRevive -= OnRevive;
                CharacterHealth.OnDeath -= OnDeath;
            }
        }

        /// <summary>
        /// �U���q�b�g���ɌĂяo���C�x���g
        /// �Ăяo���p�x�������C�x���g�̓p�t�H�[�}���X�̂��߃f���Q�[�g���g��Ȃ�
        /// </summary>
        public virtual void OnAttack(MyCharacter hitCharacter)
        {
            foreach ( var ability in _characterAbilities )
            {
                ability.OnAttack(hitCharacter);
            }
        }

        /// <summary>
        /// ��e���ɌĂяo���C�x���g
        /// �Ăяo���p�x�������C�x���g�̓p�t�H�[�}���X�̂��߃f���Q�[�g���g��Ȃ�
        /// </summary>
        public virtual void OnDamage(MyCharacter attacker)
        {
            foreach ( var ability in _characterAbilities )
            {
                ability.OnDamage(attacker);
            }
        }

        #endregion �C�x���g

        #endregion �R�[�M�[�R���g���[���[���\�b�h

        #region �L�����R���g���[���[���\�b�h

        /// <summary>
        /// �s���𔻒f���郁�\�b�h�B
        /// </summary>
        protected void MoveJudgeAct()
        {
            // 50%�̊m���ō��E�ړ��̕������ς��B
            // moveDirection = (UnityEngine.Random.Range(0, 100) >= 50) ? 1 : -1;

            //  rb.linearVelocityX = moveDirection * status.xSpeed;

            //Debug.Log($"���l�F{moveDirection * status.xSpeed} ���x�F{rb.linearVelocityX}");

            //lastJudge = GameManager.instance.NowTime;
            this.judgeCount++;
        }

        /// <summary>
        /// �^�[�Q�b�g�����߂čU������B
        /// </summary>
        protected void Attackct()
        {

        }

        /// <summary>
        /// �߂���͈͒T�����ēG�����擾���鏈���̂ЂȌ^
        /// �����Ŕ͈͓��Ɏ擾�����L������10�̂܂Ńo�b�t�@���ċ������Ń\�[�g
        /// �ЂƂ܂�������
        /// </summary>
        public void NearSearch()
        {
            unsafe
            {
                // �X�^�b�N�Ƀo�b�t�@���m�ہi10�̂܂Łj
                Span<RaycastHit2D> results = stackalloc RaycastHit2D[10];

                //int hitCount = Physics.SphereCastNonAlloc(
                //    AIManager.instance.charaDataDictionary[objecthash].liveData.nowPosition,
                //    20,
                //    results,
                //    0
                //);

            }

        }


        #endregion �L�����R���g���[���[���\�b�h

    }
}