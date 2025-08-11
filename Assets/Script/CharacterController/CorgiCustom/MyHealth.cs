using MoreMountains.CorgiEngine;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMountains.CorgiEngine
{

    /// <summary>
    /// ���̃N���X�̓I�u�W�F�N�g�̃w���X�i�̗́j���Ǘ����A�w���X�o�[�𐧌䂵�A�_���[�W���󂯂��ۂ̏����A
    /// ����ю��S���̏�����S�����܂��B
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Core/MyHealth")]
    public class MyHealth : Health
    {
        [MMInspectorGroup("�X�e�[�^�X", true, 1)]

        /// �L�����N�^�[�̌��݂̃w���X
        [MMReadOnly]
        [Tooltip("�L�����N�^�[�̌��݂̃w���X")]
        public float CurrentHealth;

        /// true�̏ꍇ�A���̃I�u�W�F�N�g�͌��݃_���[�W���󂯂邱�Ƃ��ł��܂���
        [MMReadOnly]
        [Tooltip("true�̏ꍇ�A���̃I�u�W�F�N�g�͌��݃_���[�W���󂯂邱�Ƃ��ł��܂���")]
        public bool TemporarilyInvulnerable = false;

        /// true�̏ꍇ�A���̃I�u�W�F�N�g�̓_���[�W��̖��G��Ԃł�
        [MMReadOnly]
        [Tooltip("true�̏ꍇ�A���̃I�u�W�F�N�g�̓_���[�W��̖��G��Ԃł�")]
        public bool PostDamageInvulnerable = false;

        [MMInformation(
            "���̃R���|�[�l���g���I�u�W�F�N�g�ɒǉ�����ƁA�w���X�������A�_���[�W���󂯁A���S����\��������܂��B",
            MoreMountains.Tools.MMInformationAttribute.InformationType.Info, false)]

        [MMInspectorGroup("�w���X", true, 2)]

        /// �I�u�W�F�N�g�̏����w���X��
        [Tooltip("�I�u�W�F�N�g�̏����w���X��")]
        public float InitialHealth = 10;

        /// �I�u�W�F�N�g�̍ő�w���X��
        [Tooltip("�I�u�W�F�N�g�̍ő�w���X��")]
        public float MaximumHealth = 10;

        /// true�̏ꍇ�A���̃I�u�W�F�N�g�̓_���[�W���󂯂܂���
        [Tooltip("true�̏ꍇ�A���̃I�u�W�F�N�g�̓_���[�W���󂯂܂���")]
        public bool Invulnerable = false;

        [MMInspectorGroup("�_���[�W", true, 3)]

        [MMInformation(
            "�����ł́A�I�u�W�F�N�g���_���[�W���󂯂��Ƃ��ɐ�������G�t�F�N�g�ƃT�E���hFX�A����эU�����󂯂��Ƃ��ɃI�u�W�F�N�g���_�ł��鎞�Ԃ��w��ł��܂��i�X�v���C�g�ł̂ݓ���j�B",
            MoreMountains.Tools.MMInformationAttribute.InformationType.Info, false)]

        /// ����Health�I�u�W�F�N�g���_���[�W���󂯂��邩�ǂ����B�ꎞ�I�Ȗ��G��ԂŃI��/�I�t���؂�ւ��Invulnerable�̏�ɁA����ŗV�Ԃ��Ƃ��ł��܂��BImmuneToDamage�͂��i���I�ȉ�����ł��B 
        [Tooltip("����Health�I�u�W�F�N�g���_���[�W���󂯂��邩�ǂ����B�ꎞ�I�Ȗ��G��ԂŃI��/�I�t���؂�ւ��Invulnerable�̏�ɁA����ŗV�Ԃ��Ƃ��ł��܂��BImmuneToDamage�͂��i���I�ȉ�����ł��B")]
        public bool ImmuneToDamage = false;

        /// �L�����N�^�[���U�����󂯂��Ƃ��ɍĐ�����MMFeedbacks
        [Tooltip("�L�����N�^�[���U�����󂯂��Ƃ��ɍĐ�����MMFeedbacks")]
        public MMFeedbacks DamageFeedbacks;

        /// true�̏ꍇ�A�v���I�ȍU�����ǂ����Ɋ֌W�Ȃ�DamageFeedback���Đ�����܂�
        [Tooltip("true�̏ꍇ�A�v���I�ȍU�����ǂ����Ɋ֌W�Ȃ�DamageFeedback���Đ�����܂�")]
        public bool TriggerDamageFeedbackOnDeath = true;

        /// true�̏ꍇ�A�_���[�W�l��MMFeedbacks��Intensity�p�����[�^�Ƃ��ēn����A�_���[�W����������ɂ�Ă�苭��ȃt�B�[�h�o�b�N���g���K�[�ł��܂�
        [Tooltip("true�̏ꍇ�A�_���[�W�l��MMFeedbacks��Intensity�p�����[�^�Ƃ��ēn����A�_���[�W����������ɂ�Ă�苭��ȃt�B�[�h�o�b�N���g���K�[�ł��܂�")]
        public bool FeedbackIsProportionalToDamage = false;

        /// �_���[�W���󂯂��Ƃ��ɃX�v���C�g�i����ꍇ�j��_�ł����邩�ǂ����H
        [Tooltip("�_���[�W���󂯂��Ƃ��ɃX�v���C�g�i����ꍇ�j��_�ł����邩�ǂ����H")]
        public bool FlickerSpriteOnHit = true;

        /// �X�v���C�g���_�ł���F
        [Tooltip("�X�v���C�g���_�ł���F")]
        [MMCondition("FlickerSpriteOnHit", true)]
        public Color FlickerColor = new Color32(255, 20, 20, 255);

        [MMInspectorGroup("�m�b�N�o�b�N", true, 6)]

        /// ���̃I�u�W�F�N�g���m�b�N�o�b�N���󂯂邱�Ƃ��ł��邩�ǂ���
        [Tooltip("���̃I�u�W�F�N�g���m�b�N�o�b�N���󂯂邱�Ƃ��ł��邩�ǂ���")]
        public bool ImmuneToKnockback = false;

        /// �󂯂��_���[�W���[���̏ꍇ�A���̃I�u�W�F�N�g���_���[�W�m�b�N�o�b�N�ɖƉu�����邩�ǂ���
        [Tooltip("�󂯂��_���[�W���[���̏ꍇ�A���̃I�u�W�F�N�g���_���[�W�m�b�N�o�b�N�ɖƉu�����邩�ǂ���")]
        public bool ImmuneToKnockbackIfZeroDamage = false;

        [MMInspectorGroup("���S", true, 7)]

        [MMInformation(
            "�����ł́A�I�u�W�F�N�g�����S�����Ƃ��ɐ�������G�t�F�N�g�A�K�p����́icorgi controller���K�v�j�A�Q�[���X�R�A�ɒǉ�����|�C���g���A����уL�����N�^�[�����X�|�[������ꏊ�i�v���C���[�L�����N�^�[�ȊO�̂݁j��ݒ�ł��܂��B",
            MoreMountains.Tools.MMInformationAttribute.InformationType.Info, false)]
        /// �L�����N�^�[�����S�����Ƃ��ɍĐ�����MMFeedbacks
        [Tooltip("�L�����N�^�[�����S�����Ƃ��ɍĐ�����MMFeedbacks")]
        public MMFeedbacks DeathFeedbacks;

        /// ���ꂪfalse�łȂ��ꍇ�A�I�u�W�F�N�g�͎�������̏�Ɏc��܂�
        [Tooltip("���ꂪfalse�łȂ��ꍇ�A�I�u�W�F�N�g�͎�������̏�Ɏc��܂�")]
        public bool DestroyOnDeath = true;

        /// �L�����N�^�[���j��܂��͖����������܂ł̎��ԁi�b�j
        [Tooltip("�L�����N�^�[���j��܂��͖����������܂ł̎��ԁi�b�j")]
        public float DelayBeforeDestruction = 0f;

        /// true�̏ꍇ�A�L�����N�^�[�����S���ɃR���W�������I�t�ɂȂ�܂�
        [Tooltip("true�̏ꍇ�A�L�����N�^�[�����S���ɃR���W�������I�t�ɂȂ�܂�")]
        public bool CollisionsOffOnDeath = true;

        /// true�̏ꍇ�A���S���ɏd�͂��I�t�ɂȂ�܂�
        [Tooltip("true�̏ꍇ�A���S���ɏd�͂��I�t�ɂȂ�܂�")]
        public bool GravityOffOnDeath = false;

        /// �I�u�W�F�N�g�̃w���X���[���ɒB�����Ƃ��Ƀv���C���[���l������|�C���g
        [Tooltip("�I�u�W�F�N�g�̃w���X���[���ɒB�����Ƃ��Ƀv���C���[���l������|�C���g")]
        public int PointsWhenDestroyed;

        /// ���ꂪfalse�ɐݒ肳��Ă���ꍇ�A�L�����N�^�[�͎��S�ꏊ�Ń��X�|�[�����A�����łȂ���Ώ����ʒu�i�V�[���J�n���j�Ɉړ�����܂�
        [Tooltip(
            "���ꂪfalse�ɐݒ肳��Ă���ꍇ�A�L�����N�^�[�͎��S�ꏊ�Ń��X�|�[�����A�����łȂ���Ώ����ʒu�i�V�[���J�n���j�Ɉړ�����܂�")]
        public bool RespawnAtInitialLocation = false;

        [MMInspectorGroup("���S���̗�", true, 10)]

        /// ���S���ɗ͂�K�p���邩�ǂ���
        [Tooltip("���S���ɗ͂�K�p���邩�ǂ���")]
        public bool ApplyDeathForce = true;

        /// �L�����N�^�[�����S�����Ƃ��ɓK�p������
        [Tooltip("�L�����N�^�[�����S�����Ƃ��ɓK�p������")]
        public Vector2 DeathForce = new Vector2(0, 10);

        /// ���S���ɃR���g���[���[�̗͂�0�ɐݒ肷�邩�ǂ���
        [Tooltip("���S���ɃR���g���[���[�̗͂�0�ɐݒ肷�邩�ǂ���")]
        public bool ResetForcesOnDeath = false;

        /// true�̏ꍇ�A�������ɐF�����Z�b�g����܂�
        [Tooltip("true�̏ꍇ�A�������ɐF�����Z�b�g����܂�")]
        public bool ResetColorOnRevive = true;
        /// �����_���[�̃V�F�[�_�[�ŐF���`����v���p�e�B�̖��O 
        [Tooltip("�����_���[�̃V�F�[�_�[�ŐF���`����v���p�e�B�̖��O")]
        [MMCondition("ResetColorOnRevive", true)]
        public string ColorMaterialPropertyName = "_Color";
        /// true�̏ꍇ�A���̃R���|�[�l���g�̓}�e���A���̃C���X�^���X�ō�Ƃ������Ƀ}�e���A���v���p�e�B�u���b�N���g�p���܂��B
        [Tooltip("true�̏ꍇ�A���̃R���|�[�l���g�̓}�e���A���̃C���X�^���X�ō�Ƃ������Ƀ}�e���A���v���p�e�B�u���b�N���g�p���܂��B")]
        public bool UseMaterialPropertyBlocks = false;

        [MMInspectorGroup("���L�w���X�ƃ_���[�W�ϐ�", true, 11)]

        /// ����Health���e����^����Character�A��̏ꍇ�͓����Q�[���I�u�W�F�N�g����I�����܂�
        [Tooltip("����Health���e����^����Character�A��̏ꍇ�͓����Q�[���I�u�W�F�N�g����I�����܂�")]
        public Character AssociatedCharacter;

        /// �ʂ�Health�R���|�[�l���g�i�ʏ�͕ʂ̃L�����N�^�[��j�A���ׂẴw���X�����_�C���N�g����܂�
        [Tooltip("�ʂ�Health�R���|�[�l���g�i�ʏ�͕ʂ̃L�����N�^�[��j�A���ׂẴw���X�����_�C���N�g����܂�")]
        public Health MasterHealth;

        /// true�̏ꍇ�AMasterHealth���g�p���A����Health�̓_���[�W���󂯂��A���ׂẴ_���[�W�����_�C���N�g����܂��Bfalse�̏ꍇ�A����Health�͎��g��Health������ꂽ�Ƃ��Ɏ��S�ł��܂�
        [Tooltip("true�̏ꍇ�AMasterHealth���g�p���A����Health�̓_���[�W���󂯂��A���ׂẴ_���[�W�����_�C���N�g����܂��Bfalse�̏ꍇ�A����Health�͎��g��Health������ꂽ�Ƃ��Ɏ��S�ł��܂�")]
        public bool OnlyDamageMaster = true;

        /// true�̏ꍇ�AMasterHealth���g�p���AMasterHealth�����S����Ƃ���Health�����S���܂�
        [Tooltip("true�̏ꍇ�AMasterHealth���g�p���AMasterHealth�����S����Ƃ���Health�����S���܂�")]
        public bool KillOnMasterHealthDeath = false;

        /// ����Health���_���[�W���󂯂��Ƃ��ɏ����Ɏg�p����DamageResistanceProcessor
        [Tooltip("����Health���_���[�W���󂯂��Ƃ��ɏ����Ɏg�p����DamageResistanceProcessor")]
        public DamageResistanceProcessor TargetDamageResistanceProcessor;

        public float LastDamage { get; set; }
        public Vector3 LastDamageDirection { get; set; }
        public bool Initialized => _initialized;
        public CorgiController AssociatedController => _controller;

        // ���X�|�[��
        public delegate void OnHitDelegate();
        public delegate void OnHitZeroDelegate();
        public delegate void OnReviveDelegate();
        public delegate void OnDeathDelegate();

        public OnDeathDelegate OnDeath;
        public OnHitDelegate OnHit;
        public OnHitZeroDelegate OnHitZero;
        public OnReviveDelegate OnRevive;

        protected CharacterHorizontalMovement _characterHorizontalMovement;
        protected Vector3 _initialPosition;
        protected Color _initialColor;
        protected Renderer _renderer;
        protected Character _character;
        protected CorgiController _controller;
        protected ProximityManaged _proximityManaged;
        protected MMHealthBar _healthBar;
        protected Collider2D _collider2D;
        protected bool _initialized = false;
        protected AutoRespawn _autoRespawn;
        protected Animator _animator;
        protected CharacterPersistence _characterPersistence = null;
        protected MaterialPropertyBlock _propertyBlock;
        protected bool _hasColorProperty = false;
        protected GameObject _thisObject;
        protected class InterruptiblesDamageOverTimeCoroutine
        {
            public Coroutine DamageOverTimeCoroutine;
            public DamageType DamageOverTimeType;
        }

        protected List<InterruptiblesDamageOverTimeCoroutine> _interruptiblesDamageOverTimeCoroutines;
        protected List<InterruptiblesDamageOverTimeCoroutine> _damageOverTimeCoroutines;

        /// <summary>
        /// Awake�ŁA�w���X�����������܂�
        /// </summary>
        protected virtual void Start()
        {
            Initialization();
            InitializeSpriteColor();
            InitializeCurrentHealth();
        }

        /// <summary>
        /// �L�p�ȃR���|�[�l���g���擾���A�_���[�W��L���ɂ��ď����F���擾���܂�
        /// </summary>
        protected virtual void Initialization()
        {
            _character = (AssociatedCharacter == null) ? this.gameObject.GetComponent<Character>() : AssociatedCharacter;

            if ( _character != null )
            {
                _thisObject = _character.gameObject;
                _characterPersistence = _character.FindAbility<CharacterPersistence>();
            }
            else
            {
                _thisObject = this.gameObject;
            }

            if ( this.gameObject.MMGetComponentNoAlloc<SpriteRenderer>() != null )
            {
                _renderer = this.gameObject.GetComponent<SpriteRenderer>();
            }

            if ( _character != null )
            {
                if ( _character.CharacterModel != null )
                {
                    if ( _character.CharacterModel.GetComponentInChildren<Renderer>() != null )
                    {
                        _renderer = _character.CharacterModel.GetComponentInChildren<Renderer>();
                    }
                }

                if ( _character.CharacterAnimator != null )
                {
                    _animator = _character.CharacterAnimator;
                }
                else
                {
                    _animator = this.gameObject.GetComponent<Animator>();
                }

                _characterHorizontalMovement = _character.FindAbility<CharacterHorizontalMovement>();
            }
            else
            {
                _animator = this.gameObject.GetComponent<Animator>();
            }

            if ( _animator != null )
            {
                _animator.logWarnings = false;
            }

            _proximityManaged = _thisObject.GetComponentInParent<ProximityManaged>();
            _autoRespawn = _thisObject.GetComponent<AutoRespawn>();
            _controller = _thisObject.GetComponent<CorgiController>();
            _healthBar = _thisObject.GetComponent<MMHealthBar>();
            _collider2D = _thisObject.GetComponent<Collider2D>();

            _interruptiblesDamageOverTimeCoroutines = new List<InterruptiblesDamageOverTimeCoroutine>();
            _damageOverTimeCoroutines = new List<InterruptiblesDamageOverTimeCoroutine>();

            _propertyBlock = new MaterialPropertyBlock();

            StoreInitialPosition();
            _initialized = true;
            DamageEnabled();
            DisablePostDamageInvulnerability();
            UpdateHealthBar(false);
            if ( _healthBar != null )
            {
                _healthBar.SetInitialActiveState();
            }
        }

        /// <summary>
        /// �w���X�������l�܂��͌��݂̒l�ɏ��������܂�
        /// </summary>
        public virtual void InitializeCurrentHealth()
        {
            if ( (MasterHealth == null) || (!OnlyDamageMaster) )
            {
                SetHealth(InitialHealth, _thisObject);
            }
            else
            {
                if ( MasterHealth.Initialized )
                {
                    SetHealth(MasterHealth.CurrentHealth, _thisObject);
                }
                else
                {
                    SetHealth(MasterHealth.InitialHealth, _thisObject);
                }
            }
        }

        public virtual void StoreInitialPosition()
        {
            _initialPosition = transform.position;
        }

        /// <summary>
        /// �L�����N�^�[�X�v���C�g�̏����F��ۑ����܂��B
        /// </summary>
        protected virtual void InitializeSpriteColor()
        {
            if ( !FlickerSpriteOnHit )
            {
                return;
            }

            if ( _renderer != null )
            {
                if ( UseMaterialPropertyBlocks && _renderer.HasPropertyBlock() )
                {
                    if ( _renderer.sharedMaterial.HasProperty(ColorMaterialPropertyName) )
                    {
                        _renderer.GetPropertyBlock(_propertyBlock);
                        _initialColor = _propertyBlock.GetColor(ColorMaterialPropertyName);
                        _renderer.SetPropertyBlock(_propertyBlock);
                    }
                }
                else
                {
                    if ( _renderer.material.HasProperty(ColorMaterialPropertyName) )
                    {
                        _hasColorProperty = true;
                        _initialColor = _renderer.material.GetColor(ColorMaterialPropertyName);
                    }
                }
            }
        }

        /// <summary>
        /// ���̃X�v���C�g�F�𕜌����܂�
        /// </summary>
        protected virtual void ResetSpriteColor()
        {
            if ( _renderer != null )
            {
                if ( UseMaterialPropertyBlocks && _renderer.HasPropertyBlock() )
                {
                    _renderer.GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetColor(ColorMaterialPropertyName, _initialColor);
                    _renderer.SetPropertyBlock(_propertyBlock);
                }
                else
                {
                    _renderer.material.SetColor(ColorMaterialPropertyName, _initialColor);
                }
            }
        }

        /// <summary>
        /// ����Health�R���|�[�l���g�����̃t���[���Ń_���[�W���󂯂邱�Ƃ��ł���ꍇ��true�A�����łȂ����false��Ԃ��܂�
        /// </summary>
        /// <returns></returns>
        public virtual bool CanTakeDamageThisFrame()
        {
            // �I�u�W�F�N�g�����G��Ԃ̏ꍇ�A���������ɏI�����܂�
            if ( Invulnerable || ImmuneToDamage )
            {
                return false;
            }

            if ( !this.enabled )
            {
                return false;
            }

            // ���łɃ[���ȉ��̏ꍇ�A���������ɏI�����܂�
            if ( (CurrentHealth <= 0) && (InitialHealth != 0) )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// �I�u�W�F�N�g���_���[�W���󂯂��Ƃ��ɌĂяo����܂�
        /// </summary>
        /// <param name="damage">������w���X�|�C���g�̗ʁB</param>
        /// <param name="instigator">�_���[�W�������N�������I�u�W�F�N�g�B</param>
        /// <param name="flickerDuration">�_���[�W���󂯂���ɃI�u�W�F�N�g���_�ł��鎞�ԁi�b�j�B</param>
        /// <param name="invincibilityDuration">�U����̒Z�����G���Ԃ̌p�����ԁB</param>
        public virtual void Damage(float damage, GameObject instigator, float flickerDuration,
            float invincibilityDuration, Vector3 damageDirection, List<TypedDamage> typedDamages = null)
        {
            if ( !gameObject.activeInHierarchy )
            {
                return;
            }

            // �I�u�W�F�N�g�����G��Ԃ̏ꍇ�A���������ɏI�����܂�
            if ( TemporarilyInvulnerable || Invulnerable || ImmuneToDamage || PostDamageInvulnerable )
            {
                OnHitZero?.Invoke();
                return;
            }

            if ( !CanTakeDamageThisFrame() )
            {
                return;
            }

            damage = ComputeDamageOutput(damage, typedDamages, true);

            // ��ԕω����������܂�
            ComputeCharacterConditionStateChanges(typedDamages);
            ComputeCharacterMovementMultipliers(typedDamages);

            if ( damage <= 0 )
            {
                OnHitZero?.Invoke();
                return;
            }

            // �L�����N�^�[�̃w���X���_���[�W�����������܂�
            float previousHealth = CurrentHealth;
            if ( MasterHealth != null )
            {
                previousHealth = MasterHealth.CurrentHealth;
                MasterHealth.Damage(damage, instigator, flickerDuration, invincibilityDuration, damageDirection, typedDamages);

                if ( !OnlyDamageMaster )
                {
                    previousHealth = CurrentHealth;
                    SetHealth(CurrentHealth - damage, instigator);
                }
            }
            else
            {
                SetHealth(CurrentHealth - damage, instigator);
            }

            LastDamage = damage;
            LastDamageDirection = damageDirection;
            OnHit?.Invoke();

            if ( CurrentHealth < 0 )
            {
                CurrentHealth = 0;
            }

            // �L�����N�^�[��Projectiles�APlayer�AEnemies�ƏՓ˂��邱�Ƃ�h���܂�
            if ( (invincibilityDuration > 0) && gameObject.activeInHierarchy )
            {
                EnablePostDamageInvulnerability();
                StartCoroutine(DisablePostDamageInvulnerability(invincibilityDuration));
            }

            // �_���[�W���󂯂��C�x���g���g���K�[���܂�
            MMDamageTakenEvent.Trigger(this, instigator, CurrentHealth, damage, previousHealth);

            if ( _animator != null )
            {
                _animator.SetTrigger("Damage");
            }

            // �_���[�W�t�B�[�h�o�b�N���Đ����܂�
            if ( TriggerDamageFeedbackOnDeath || CurrentHealth != 0 )
            {
                if ( FeedbackIsProportionalToDamage )
                {
                    DamageFeedbacks?.PlayFeedbacks(this.transform.position, damage);
                }
                else
                {
                    DamageFeedbacks?.PlayFeedbacks(this.transform.position);
                }
            }

            if ( FlickerSpriteOnHit )
            {
                // �L�����N�^�[�̃X�v���C�g��_�ł����܂�
                if ( _renderer != null )
                {
                    StartCoroutine(MMImage.Flicker(_renderer, _initialColor, FlickerColor, 0.05f, flickerDuration));
                }
            }

            // �w���X�o�[���X�V���܂�
            UpdateHealthBar(true);

            // �w���X���[���ɒB�����ꍇ�A�w���X���[���ɐݒ肵�܂��i�w���X�o�[�ɗL�p�j
            if ( MasterHealth != null )
            {
                if ( MasterHealth.CurrentHealth <= 0 )
                {
                    MasterHealth.CurrentHealth = 0;
                    Kill();
                }
                if ( !OnlyDamageMaster )
                {
                    if ( CurrentHealth <= 0 )
                    {
                        CurrentHealth = 0;
                        Kill();
                    }
                }
            }
            else
            {
                if ( CurrentHealth <= 0 )
                {
                    CurrentHealth = 0;
                    Kill();
                }
            }
        }

        /// <summary>
        /// �_���[�W��K�p�����AOnHitZero���g���K�[���܂�
        /// </summary>
        public virtual void DamageZero()
        {
            if ( !gameObject.activeInHierarchy )
            {
                return;
            }
            OnHitZero?.Invoke();
        }

        /// <summary>
        /// �L�����N�^�[���E���A���S�G�t�F�N�g�𐶐����A�|�C���g����������Ȃ�
        /// </summary>
        public virtual void Kill()
        {
            if ( ImmuneToDamage )
            {
                return;
            }

            if ( _character != null )
            {
                // ���S��Ԃ�true�ɐݒ肵�܂�
                _character.ConditionState.ChangeState(CharacterStates.CharacterConditions.Dead);
                _character.Reset();

                if ( _character.CharacterType == Character.CharacterTypes.Player )
                {
                    CorgiEngineEvent.Trigger(CorgiEngineEventTypes.PlayerDeath, _character);
                }
            }
            SetHealth(0f, _thisObject);

            // ����Ȃ�_���[�W��h���܂�
            DamageDisabled();

            StopAllDamageOverTime();

            // �j��G�t�F�N�g�𐶐����܂�
            DeathFeedbacks?.PlayFeedbacks();

            // �K�v�ɉ����ă|�C���g��ǉ����܂��B
            if ( PointsWhenDestroyed != 0 )
            {
                // GameManager���L���b�`����V�����|�C���g�C�x���g�𑗐M���܂��i����т���𕷂����̃N���X���j
                CorgiEnginePointsEvent.Trigger(PointsMethods.Add, PointsWhenDestroyed);
            }

            if ( _animator != null )
            {
                _animator.SetTrigger("Death");
            }

            if ( OnDeath != null )
            {
                OnDeath();
            }

            MMLifeCycleEvent.Trigger(this, MMLifeCycleEventTypes.Death);

            HealthDeathEvent.Trigger(this);

            // �R���g���[���[������ꍇ�A�Փ˂��폜���A���X�|�[���̂��߂̃p�����[�^�𕜌����A���S�͂�K�p���܂�
            if ( _controller != null )
            {
                // ����͏Փ˂𖳎������܂�
                if ( CollisionsOffOnDeath )
                {
                    _controller.CollisionsOff();
                    if ( _collider2D != null )
                    {
                        _collider2D.enabled = false;
                    }
                }

                // �p�����[�^�����Z�b�g���܂�
                _controller.ResetParameters();

                if ( GravityOffOnDeath )
                {
                    _controller.GravityActive(false);
                }

                // �K�v�ɉ����Ď��S���ɃR���g���[���[�̗͂����Z�b�g���܂�
                if ( ResetForcesOnDeath )
                {
                    _controller.SetForce(Vector2.zero);
                }

                // ���S�͂�K�p���܂�
                if ( ApplyDeathForce )
                {
                    _controller.GravityActive(true);
                    _controller.SetForce(DeathForce);
                }
            }


            // �L�����N�^�[������ꍇ�A���̏�Ԃ�ύX�������Ǝv���܂�
            if ( _character != null )
            {
                // ���S��Ԃ�true�ɐݒ肵�܂�
                _character.ConditionState.ChangeState(CharacterStates.CharacterConditions.Dead);
                _character.Reset();

                // ���ꂪ�v���C���[�̏ꍇ�A�����ŏI�����܂�
                if ( _character.CharacterType == Character.CharacterTypes.Player )
                {
                    return;
                }
            }

            if ( DelayBeforeDestruction > 0f )
            {
                Invoke("DestroyObject", DelayBeforeDestruction);
            }
            else
            {
                // �Ō�ɃI�u�W�F�N�g��j�󂵂܂�
                DestroyObject();
            }
        }

        /// <summary>
        /// ���̃I�u�W�F�N�g�𕜊������܂��B
        /// </summary>
        public virtual void Revive()
        {
            if ( !_initialized )
            {
                return;
            }

            if ( _characterPersistence != null )
            {
                if ( _characterPersistence.Initialized )
                {
                    return;
                }
            }

            if ( _collider2D != null )
            {
                _collider2D.enabled = true;
            }

            if ( _controller != null )
            {
                _controller.CollisionsOn();
                _controller.GravityActive(true);
                _controller.SetForce(Vector2.zero);
                _controller.ResetParameters();
            }

            if ( _character != null )
            {
                _character.ConditionState.ChangeState(CharacterStates.CharacterConditions.Normal);
            }

            if ( RespawnAtInitialLocation )
            {
                transform.position = _initialPosition;
            }

            Initialization();
            InitializeCurrentHealth();
            if ( FlickerSpriteOnHit && ResetColorOnRevive )
            {
                ResetSpriteColor();
            }

            UpdateHealthBar(false);
            if ( _healthBar != null )
            {
                _healthBar.SetInitialActiveState();
            }
            if ( OnRevive != null )
            {
                OnRevive.Invoke();
            }
            MMLifeCycleEvent.Trigger(this, MMLifeCycleEventTypes.Revive);
        }

        /// <summary>
        /// �L�����N�^�[�̐ݒ�ɉ����āA�I�u�W�F�N�g��j�󂷂邩�A�j������݂܂�
        /// </summary>
        protected virtual void DestroyObject()
        {
            if ( !DestroyOnDeath )
            {
                return;
            }

            if ( _autoRespawn == null )
            {
                // �I�u�W�F�N�g�́A���X�|�[�����ɕ����ł���悤�ɔ�A�N�e�B�u�ɂȂ�܂�
                gameObject.SetActive(false);
            }
            else
            {
                _autoRespawn.Kill();
            }
        }

        /// <summary>
        /// �^�C�v�Ɋ֌W�Ȃ��A���ׂĂ̌p���_���[�W�𒆒f���܂�
        /// </summary>
        public virtual void InterruptAllDamageOverTime()
        {
            foreach ( InterruptiblesDamageOverTimeCoroutine coroutine in _interruptiblesDamageOverTimeCoroutines )
            {
                StopCoroutine(coroutine.DamageOverTimeCoroutine);
            }
            _interruptiblesDamageOverTimeCoroutines.Clear();
        }

        /// <summary>
        /// ���f�s�\�Ȃ��̂��܂߁A���ׂĂ̌p���_���[�W�𒆒f���܂��i�ʏ펀�S���j
        /// </summary>
        public virtual void StopAllDamageOverTime()
        {
            foreach ( InterruptiblesDamageOverTimeCoroutine coroutine in _damageOverTimeCoroutines )
            {
                StopCoroutine(coroutine.DamageOverTimeCoroutine);
            }
            _damageOverTimeCoroutines.Clear();
        }

        /// <summary>
        /// �w�肳�ꂽ�^�C�v�̂��ׂĂ̌p���_���[�W�𒆒f���܂�
        /// </summary>
        /// <param name="damageType"></param>
        public virtual void InterruptAllDamageOverTimeOfType(DamageType damageType)
        {
            foreach ( InterruptiblesDamageOverTimeCoroutine coroutine in _interruptiblesDamageOverTimeCoroutines )
            {
                if ( coroutine.DamageOverTimeType == damageType )
                {
                    StopCoroutine(coroutine.DamageOverTimeCoroutine);
                }
            }
            TargetDamageResistanceProcessor?.InterruptDamageOverTime(damageType);
        }

        /// <summary>
        /// �w�肳�ꂽ�񐔂̌J��Ԃ��i�ŏ��̃_���[�W�K�p���܂ށA�C���X�y�N�^�[�ł̊ȒP�Ȍv�Z�̂��߁j�Ǝw�肳�ꂽ�Ԋu�Ōp���_���[�W��K�p���܂��B
        /// �I�v�V�����ŁA�_���[�W�����f�\���ǂ���������ł��܂��B���̏ꍇ�AInterruptAllDamageOverTime()���Ăяo���Ƃ����̓K�p����~����A�ł����Â���ꍇ�ȂǂɗL�p�ł��B
        /// </summary>
        /// <param name="damage"></param>
        /// <param name="instigator"></param>
        /// <param name="flickerDuration"></param>
        /// <param name="invincibilityDuration"></param>
        /// <param name="damageDirection"></param>
        /// <param name="typedDamages"></param>
        /// <param name="amountOfRepeats"></param>
        /// <param name="durationBetweenRepeats"></param>
        /// <param name="interruptible"></param>
        public virtual void DamageOverTime(float damage, GameObject instigator, float flickerDuration,
            float invincibilityDuration, Vector3 damageDirection, List<TypedDamage> typedDamages = null,
            int amountOfRepeats = 0, float durationBetweenRepeats = 1f, bool interruptible = true, DamageType damageType = null)
        {
            if ( ComputeDamageOutput(damage, typedDamages, false) == 0 )
            {
                return;
            }

            InterruptiblesDamageOverTimeCoroutine damageOverTime = new InterruptiblesDamageOverTimeCoroutine();
            damageOverTime.DamageOverTimeType = damageType;
            damageOverTime.DamageOverTimeCoroutine = StartCoroutine(DamageOverTimeCo(damage, instigator, flickerDuration,
                invincibilityDuration, damageDirection, typedDamages, amountOfRepeats, durationBetweenRepeats,
                interruptible));

            _damageOverTimeCoroutines.Add(damageOverTime);

            if ( interruptible )
            {
                _interruptiblesDamageOverTimeCoroutines.Add(damageOverTime);
            }
        }

        /// <summary>
        /// �p���_���[�W��K�p���邽�߂Ɏg�p�����R���[�`��
        /// </summary>
        /// <param name="damage"></param>
        /// <param name="instigator"></param>
        /// <param name="flickerDuration"></param>
        /// <param name="invincibilityDuration"></param>
        /// <param name="damageDirection"></param>
        /// <param name="typedDamages"></param>
        /// <param name="amountOfRepeats"></param>
        /// <param name="durationBetweenRepeats"></param>
        /// <param name="interruptible"></param>
        /// <param name="damageType"></param>
        /// <returns></returns>
        protected virtual IEnumerator DamageOverTimeCo(float damage, GameObject instigator, float flickerDuration,
            float invincibilityDuration, Vector3 damageDirection, List<TypedDamage> typedDamages = null,
            int amountOfRepeats = 0, float durationBetweenRepeats = 1f, bool interruptible = true, DamageType damageType = null)
        {
            for ( int i = 0; i < amountOfRepeats; i++ )
            {
                Damage(damage, instigator, flickerDuration, invincibilityDuration, damageDirection, typedDamages);
                yield return MMCoroutine.WaitFor(durationBetweenRepeats);
            }
        }

        /// <summary>
        /// ���ݓI�ȑϐ�������������A���̃w���X���󂯂�ׂ��_���[�W��Ԃ��܂�
        /// </summary>
        /// <param name="damage"></param>
        /// <returns></returns>
        public virtual float ComputeDamageOutput(float damage, List<TypedDamage> typedDamages = null, bool damageApplied = false)
        {
            if ( TemporarilyInvulnerable || Invulnerable || ImmuneToDamage || PostDamageInvulnerable )
            {
                return 0;
            }

            float totalDamage = 0f;
            // ���ݓI�ȑϐ���ʂ��ă_���[�W���������܂�
            if ( TargetDamageResistanceProcessor != null )
            {
                if ( TargetDamageResistanceProcessor.isActiveAndEnabled )
                {
                    totalDamage = TargetDamageResistanceProcessor.ProcessDamage(damage, typedDamages, damageApplied);
                }
            }
            else
            {
                totalDamage = damage;
                if ( typedDamages != null )
                {
                    foreach ( TypedDamage typedDamage in typedDamages )
                    {
                        totalDamage += typedDamage.DamageCaused;
                    }
                }
            }
            return totalDamage;
        }

        /// <summary>
        /// �ϐ���ʂ��ď������邱�ƂŐV�����m�b�N�o�b�N�͂����肵�܂�
        /// </summary>
        /// <param name="knockbackForce"></param>
        /// <param name="typedDamages"></param>
        /// <returns></returns>
        public virtual Vector2 ComputeKnockbackForce(Vector2 knockbackForce, List<TypedDamage> typedDamages = null)
        {
            return (TargetDamageResistanceProcessor == null) ? knockbackForce : TargetDamageResistanceProcessor.ProcessKnockbackForce(knockbackForce, typedDamages);
            ;

        }

        /// <summary>
        /// ����Health���m�b�N�o�b�N���󂯂邱�Ƃ��ł���ꍇ��true�A�����łȂ����false��Ԃ��܂�
        /// </summary>
        /// <param name="typedDamages"></param>
        /// <returns></returns>
        public virtual bool CanGetKnockback(List<TypedDamage> typedDamages)
        {
            if ( ImmuneToKnockback )
            {
                return false;
            }
            if ( TargetDamageResistanceProcessor != null )
            {
                if ( TargetDamageResistanceProcessor.isActiveAndEnabled )
                {
                    bool checkResistance = TargetDamageResistanceProcessor.CheckPreventKnockback(typedDamages);
                    if ( checkResistance )
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// �ϐ���ʂ��ď������A�K�v�ɉ����ď�ԕω���K�p���܂�
        /// </summary>
        /// <param name="typedDamages"></param>
        protected virtual void ComputeCharacterConditionStateChanges(List<TypedDamage> typedDamages)
        {
            if ( (typedDamages == null) || (_character == null) )
            {
                return;
            }

            foreach ( TypedDamage typedDamage in typedDamages )
            {
                if ( typedDamage.ForceCharacterCondition )
                {
                    if ( TargetDamageResistanceProcessor != null )
                    {
                        if ( TargetDamageResistanceProcessor.isActiveAndEnabled )
                        {
                            bool checkResistance =
                                TargetDamageResistanceProcessor.CheckPreventCharacterConditionChange(typedDamage.AssociatedDamageType);
                            if ( checkResistance )
                            {
                                continue;
                            }
                        }
                    }
                    _character.ChangeCharacterConditionTemporarily(typedDamage.ForcedCondition, typedDamage.ForcedConditionDuration, typedDamage.ResetControllerForces, typedDamage.DisableGravity);
                }
            }
        }

        /// <summary>
        /// �ϐ����X�g��ʂ��ď������A�K�v�ɉ����Ĉړ��{����K�p���܂�
        /// </summary>
        /// <param name="typedDamages"></param>
        protected virtual void ComputeCharacterMovementMultipliers(List<TypedDamage> typedDamages)
        {
            if ( (typedDamages == null) || (_character == null) )
            {
                return;
            }

            foreach ( TypedDamage typedDamage in typedDamages )
            {
                if ( typedDamage.ApplyMovementMultiplier )
                {
                    if ( TargetDamageResistanceProcessor != null )
                    {
                        if ( TargetDamageResistanceProcessor.isActiveAndEnabled )
                        {
                            bool checkResistance =
                                TargetDamageResistanceProcessor.CheckPreventMovementModifier(typedDamage.AssociatedDamageType);
                            if ( checkResistance )
                            {
                                continue;
                            }
                        }
                    }

                    _characterHorizontalMovement?.ApplyContextSpeedMultiplier(typedDamage.MovementMultiplier, typedDamage.MovementMultiplierDuration);
                }
            }

        }


        /// <summary>
        /// �L�����N�^�[���w���X���擾�����Ƃ��ɌĂ΂�܂��i��F�X�e�B���p�b�N����j
        /// </summary>
        /// <param name="health">�L�����N�^�[���擾����w���X�B</param>
        /// <param name="instigator">�L�����N�^�[�Ƀw���X��^������́B</param>
        public virtual void GetHealth(float health, GameObject instigator)
        {
            // ���̊֐��̓L�����N�^�[��Health�Ƀw���X��ǉ����AMaxHealth�𒴂��邱�Ƃ�h���܂��B
            if ( MasterHealth != null )
            {
                MasterHealth.SetHealth(Mathf.Min(CurrentHealth + health, MaximumHealth), instigator);
            }
            else
            {
                SetHealth(Mathf.Min(CurrentHealth + health, MaximumHealth), instigator);
            }
            UpdateHealthBar(true);
        }

        /// <summary>
        /// �L�����N�^�[�̃w���X���p�����[�^�Ŏw�肳�ꂽ���̂ɐݒ肵�܂�
        /// </summary>
        /// <param name="newHealth"></param>
        /// <param name="instigator"></param>
        public virtual void SetHealth(float newHealth, GameObject instigator)
        {
            CurrentHealth = Mathf.Min(newHealth, MaximumHealth);
            UpdateHealthBar(false);
            HealthChangeEvent.Trigger(this, newHealth);
        }

        /// <summary>
        /// �L�����N�^�[�̃w���X���ő�l�Ƀ��Z�b�g���܂�
        /// </summary>
        public virtual void ResetHealthToMaxHealth()
        {
            CurrentHealth = MaximumHealth;
            UpdateHealthBar(false);
            HealthChangeEvent.Trigger(this, CurrentHealth);
        }

        /// <summary>
        /// �L�����N�^�[�̃w���X�o�[�̐i�s�󋵂��X�V���܂��B
        /// </summary>
        public virtual void UpdateHealthBar(bool show)
        {
            if ( _healthBar != null )
            {
                _healthBar.UpdateBar(CurrentHealth, 0f, MaximumHealth, show);
            }

            if ( _character != null )
            {
                if ( _character.CharacterType == Character.CharacterTypes.Player )
                {
                    // �w���X�o�[���X�V���܂�
                    if ( GUIManager.HasInstance )
                    {
                        GUIManager.Instance.UpdateHealthBar(CurrentHealth, 0f, MaximumHealth, _character.PlayerID);
                    }
                }
            }
        }

        /// <summary>
        /// �L�����N�^�[���_���[�W���󂯂邱�Ƃ�h���܂�
        /// </summary>
        public virtual void DamageDisabled()
        {
            TemporarilyInvulnerable = true;
        }

        /// <summary>
        /// �L�����N�^�[���_���[�W���󂯂邱�Ƃ������܂�
        /// </summary>
        public virtual void DamageEnabled()
        {
            TemporarilyInvulnerable = false;
        }

        /// <summary>
        /// �L�����N�^�[���_���[�W���󂯂邱�Ƃ�h���܂�
        /// </summary>
        public virtual void EnablePostDamageInvulnerability()
        {
            PostDamageInvulnerable = true;
        }

        /// <summary>
        /// �L�����N�^�[���_���[�W���󂯂邱�Ƃ������܂�
        /// </summary>
        public virtual void DisablePostDamageInvulnerability()
        {
            PostDamageInvulnerable = false;
        }

        /// <summary>
        /// �L�����N�^�[���_���[�W���󂯂邱�Ƃ������܂�
        /// </summary>
        public virtual IEnumerator DisablePostDamageInvulnerability(float delay)
        {
            yield return MMCoroutine.WaitFor(delay);
            PostDamageInvulnerable = false;
        }

        /// <summary>
        /// �w�肳�ꂽ�x����ɃL�����N�^�[���Ăу_���[�W���󂯂邱�Ƃ��ł���悤�ɂȂ�܂�
        /// </summary>
        /// <returns>���C���[�ՓˁB</returns>
        public virtual IEnumerator DamageEnabled(float delay)
        {
            yield return MMCoroutine.WaitFor(delay);
            TemporarilyInvulnerable = false;
        }

        /// <summary>
        /// �I�u�W�F�N�g���L�������ꂽ�Ƃ��i��F���X�|�[�����j�A�����w���X���x���𕜌����܂�
        /// </summary>
        protected virtual void OnEnable()
        {
            if ( (_characterPersistence != null) && (_characterPersistence.Initialized) )
            {
                UpdateHealthBar(false);
                return;
            }

            this.MMEventStartListening<HealthDeathEvent>();

            if ( (_proximityManaged != null) && _proximityManaged.StateChangedThisFrame )
            {
                return;
            }
            InitializeCurrentHealth();
            DamageEnabled();
            DisablePostDamageInvulnerability();
            UpdateHealthBar(false);
        }

        /// <summary>
        /// ���������Ɏ��s���̂��ׂĂ�invoke���L�����Z�����܂�
        /// </summary>
        protected virtual void OnDisable()
        {
            CancelInvoke();
            this.MMEventStopListening<HealthDeathEvent>();
        }

        public void OnMMEvent(HealthDeathEvent deathEvent)
        {
            if ( KillOnMasterHealthDeath && (deathEvent.AffectedHealth == MasterHealth) )
            {
                Kill();
            }
        }
    }

}