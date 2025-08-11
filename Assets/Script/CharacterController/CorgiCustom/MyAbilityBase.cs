using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// �L�����N�^�[�̃A�r���e�B���������邽�߂̃I�[�o�[���C�h�\�ȃx�[�X�N���X
    /// ���̃N���X���p�����ēƎ��̃A�r���e�B���쐬����
    /// </summary>
    /// 
    public class MyAbilityBase : CharacterAbility
    {
        #region �����E�����ݒ�

        /// <summary>
        /// �A�r���e�B�̎��s���u���b�N���镐���Ԃ̔z��
        /// �L�����N�^�[�̕��킪�����̏�Ԃ̎��ɃA�r���e�B�𔭓����悤�Ƃ��Ă�������Ȃ�
        /// ��F�U�����ɃA�r���e�B���g�p�s�ɂ���
        /// 
        /// �s�v�ɂ��B��
        /// </summary>
        [HideInInspector]
        [Tooltip("�A�r���e�B�̎��s���u���b�N���镐���Ԃ̔z��B�L�����N�^�[�̕��킪�����̏�Ԃ̎��ɃA�r���e�B�𔭓����悤�Ƃ��Ă�������Ȃ��B��F�U�����ɃA�r���e�B���g�p�s�ɂ���")]
        public new Weapon.WeaponStates[] BlockingWeaponStates;

        #endregion

        #region �����ϐ��E�R���|�[�l���g�Q��

        // �L�����N�^�[�֘A�̎Q��
        protected new MyCharacter _character;                              // �e�L�����N�^�[�̎Q��
        protected new MyHealth _health;                                    // �w���X�Ǘ��R���|�[�l���g
        protected CharacterHorizontalMovement _characterHorizontalMovement; // �����ړ��A�r���e�B
        protected new MyCorgiController _controller;                       // Corgi�R���g���[���[

        // ��ԊǗ�
        protected new MyConditionStateMachine _condition; // �R���f�B�V������ԃ}�V��

        #endregion

        #region �v���p�e�B

        /// <summary>
        /// �A�r���e�B�����s�\���ǂ����𔻒肷��v���p�e�B
        /// �e��u���b�L���O��Ԃ��`�F�b�N���A�ŏI�I��AbilityPermitted�̒l��Ԃ�
        /// </summary>
        public override bool AbilityAuthorized
        {
            get
            {
                // �L���������Ȃ����A�r���e�B��������ĂȂ��Ȃ瑁�����^�[��
                if ( !AbilityPermitted || _character == null )
                {
                    return false;
                }

                // �ړ���Ԃ̃u���b�N�`�F�b�N
                if ( ((uint)(_movement.CurrentState) & _blockingMovementBit) > 0 )
                {
                    return false;
                }

                // �R���f�B�V������Ԃ̃u���b�N�`�F�b�N
                if ( (((uint)_condition.CurrentState) & _blockingConditionBit) > 0 )
                {
                    return false;
                }

                // ���ׂẴ`�F�b�N��ʉ߂����ꍇ��AbilityPermitted�̒l��Ԃ�
                return true;
            }
        }

        #endregion

        #region �����ϐ��E�R���|�[�l���g�Q��

        /// <summary>
        /// �A�r���e�B�֎~��Ԃ�ێ����邽�߂̃r�b�g�t���O
        /// </summary>
        private uint _blockingConditionBit = 0;

        /// <summary>
        /// �A�r���e�B�֎~�s����Ԃ�ێ����邽�߂̃r�b�g�t���O
        /// </summary>
        private uint _blockingMovementBit = 0;

        #endregion

        #region �������E�Z�b�g�A�b�v

        /// <summary>
        /// �K�v�ȃR���|�[�l���g���擾�E�ۑ����ď��������s��
        /// ���̃��\�b�h�Ŋe��Q�Ƃ�ݒ肵�A�A�r���e�B���g�p�\�ȏ�Ԃɂ���
        /// 
        /// ����Ɋւ��Ă͕K��base�����s����
        /// </summary>
        protected override void Initialization()
        {
            // �e�I�u�W�F�N�g����e��R���|�[�l���g���擾
            _character = this.gameObject.GetComponentInParent<MyCharacter>();
            _controller = this.gameObject.GetComponentInParent<MyCorgiController>();

            // �L�����N�^�[����֘A�A�r���e�B���擾
            _characterHorizontalMovement = _character.FindAbility<CharacterHorizontalMovement>();
            _characterGravity = _character.FindAbility<CharacterGravity>();
            _health = _character.CharacterHealth;

            // �A�j���[�^�[�̐ݒ�
            BindAnimator();

            // �L�����N�^�[�����݂���ꍇ�A�e��Q�Ƃ�ݒ�
            if ( _character != null )
            {
                _characterTransform = _character.transform;
                _sceneCamera = _character.SceneCamera;
                _inputManager = _character.LinkedInputManager;
                _state = _character.CharacterState;
                _movement = _character.MovementState;
                _condition = _character.ConditionState;
            }

            //������Ԕ��f�p��bit��������
            for ( int i = 0; i < BlockingConditionStates.Length; i++ )
            {
                _blockingConditionBit &= (uint)(1 << (int)BlockingConditionStates[i]);
            }

            //�����s�����f�p��bit��������
            for ( int i = 0; i < BlockingMovementStates.Length; i++ )
            {
                _blockingMovementBit &= (uint)(1 << (int)BlockingMovementStates[i]);
            }

            // �����������t���O��ݒ�
            _abilityInitialized = true;
        }

        #endregion

        #region ���͏���


        #endregion

        #region �A�r���e�B�����t�F�[�Y

        /// <summary>
        /// �A�r���e�B��3�̃p�X�̍ŏ��̃p�X
        /// EarlyUpdate()�̂悤�Ȃ��̂ƍl����B��ɓ��͏������s��
        /// </summary>
        //public virtual void EarlyProcessAbility()
        //{
        //    InternalHandleInput();
        //}

        /// <summary>
        /// �A�r���e�B��3�̃p�X��2�Ԗڂ̃p�X
        /// Update()�̂悤�Ȃ��̂ƍl����B���C���̏������s��
        /// </summary>
        //public virtual void ProcessAbility()
        //{
        //    // �p����Ŏ���
        //}

        /// <summary>
        /// �A�r���e�B��3�̃p�X�̍Ō�̃p�X
        /// LateUpdate()�̂悤�Ȃ��̂ƍl����B�㏈�����s��
        /// </summary>
        //public virtual void LateProcessAbility()
        //{
        //    // �p����Ŏ���
        //}

        /// <summary>
        /// �L�����N�^�[�̃A�j���[�^�[�Ƀp�����[�^�[�𑗐M���邽�߂ɃI�[�o�[���C�h����
        /// Early�A�ʏ�ALate�̊eprocess()�̌�ɁACharacter�N���X�ɂ����1�T�C�N����1��Ă΂��
        /// </summary>
        //public virtual void UpdateAnimator()
        //{
        //    // �p����Ŏ���
        //}

        #endregion

        #region �A�r���e�B���䃁�\�b�h

        /// <summary>
        /// �A�r���e�B�̋���Ԃ�ύX����
        /// </summary>
        /// <param name="abilityPermitted">true�̏ꍇ�A�r���e�B������</param>
        //public virtual void PermitAbility(bool abilityPermitted)
        //{
        //    AbilityPermitted = abilityPermitted;
        //}

        /// <summary>
        /// �L�����N�^�[�����]�������ɂ��̃A�r���e�B�ŉ����N���邩���w�肷�邽�߂ɃI�[�o�[���C�h����
        /// </summary>
        //public virtual void Flip()
        //{
        //    // �p����Ŏ���
        //}

        /// <summary>
        /// ���̃A�r���e�B�̃p�����[�^�[�����Z�b�g���邽�߂ɃI�[�o�[���C�h����
        /// �L�����N�^�[���|���ꂽ�Ƃ��A���X�|�[���̏����Ƃ��Ď����I�ɌĂ΂��
        /// </summary>
        //public virtual void ResetAbility()
        //{
        //    // �p����Ŏ���
        //}

        #endregion

        #region �C�x���g�n���h���[

        /// <summary>
        /// �L�����N�^�[�����X�|�[���������ɂ��̃A�r���e�B�ɉ����N���邩���L�q���邽�߂ɃI�[�o�[���C�h����
        /// 
        /// OnEnable()�C�x���g�ɋ߂�
        /// </summary>
        //protected virtual void OnRespawn()
        //{
        //    // �p����Ŏ���
        //}

        /// <summary>
        /// �L�����N�^�[�����S�������ɂ��̃A�r���e�B�ɉ����N���邩���L�q���邽�߂ɃI�[�o�[���C�h����
        /// �f�t�H���g�ł͊J�n�t�B�[�h�o�b�N���~����
        /// 
        /// Disenable()�C�x���g�ɋ߂�
        /// </summary>
        //protected virtual void OnDeath()
        //{
        //    StopStartFeedbacks();
        //}

        /// <summary>
        /// �L�����N�^�[���q�b�g���󂯂����ɂ��̃A�r���e�B�ɉ����N���邩���L�q���邽�߂ɃI�[�o�[���C�h����
        /// �U���󂯂����̃C�x���g
        /// </summary>
        //protected virtual void OnHit()
        //{
        //    // �p����Ŏ���
        //}

        /// <summary>
        /// �U�����q�b�g�������Ƃ�DamageOnTouch���甭�΂���C�x���g
        /// </summary>  
        public virtual void OnAttack(MyCharacter hitCharacter)
        {

        }

        /// <summary>
        /// �U�����q�b�g�����Ƃ�Health���甭�΂���C�x���g
        /// </summary>  
        public virtual void OnDamage(MyCharacter attacker)
        {

        }

        #endregion

    }
}