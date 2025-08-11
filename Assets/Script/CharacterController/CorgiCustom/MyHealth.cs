using MoreMountains.CorgiEngine;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMountains.CorgiEngine
{

    /// <summary>
    /// このクラスはオブジェクトのヘルス（体力）を管理し、ヘルスバーを制御し、ダメージを受けた際の処理、
    /// および死亡時の処理を担当します。
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Core/MyHealth")]
    public class MyHealth : Health
    {
        [MMInspectorGroup("ステータス", true, 1)]

        /// キャラクターの現在のヘルス
        [MMReadOnly]
        [Tooltip("キャラクターの現在のヘルス")]
        public float CurrentHealth;

        /// trueの場合、このオブジェクトは現在ダメージを受けることができません
        [MMReadOnly]
        [Tooltip("trueの場合、このオブジェクトは現在ダメージを受けることができません")]
        public bool TemporarilyInvulnerable = false;

        /// trueの場合、このオブジェクトはダメージ後の無敵状態です
        [MMReadOnly]
        [Tooltip("trueの場合、このオブジェクトはダメージ後の無敵状態です")]
        public bool PostDamageInvulnerable = false;

        [MMInformation(
            "このコンポーネントをオブジェクトに追加すると、ヘルスを持ち、ダメージを受け、死亡する可能性があります。",
            MoreMountains.Tools.MMInformationAttribute.InformationType.Info, false)]

        [MMInspectorGroup("ヘルス", true, 2)]

        /// オブジェクトの初期ヘルス量
        [Tooltip("オブジェクトの初期ヘルス量")]
        public float InitialHealth = 10;

        /// オブジェクトの最大ヘルス量
        [Tooltip("オブジェクトの最大ヘルス量")]
        public float MaximumHealth = 10;

        /// trueの場合、このオブジェクトはダメージを受けません
        [Tooltip("trueの場合、このオブジェクトはダメージを受けません")]
        public bool Invulnerable = false;

        [MMInspectorGroup("ダメージ", true, 3)]

        [MMInformation(
            "ここでは、オブジェクトがダメージを受けたときに生成するエフェクトとサウンドFX、および攻撃を受けたときにオブジェクトが点滅する時間を指定できます（スプライトでのみ動作）。",
            MoreMountains.Tools.MMInformationAttribute.InformationType.Info, false)]

        /// このHealthオブジェクトがダメージを受けられるかどうか。一時的な無敵状態でオン/オフが切り替わるInvulnerableの上に、これで遊ぶことができます。ImmuneToDamageはより永続的な解決策です。 
        [Tooltip("このHealthオブジェクトがダメージを受けられるかどうか。一時的な無敵状態でオン/オフが切り替わるInvulnerableの上に、これで遊ぶことができます。ImmuneToDamageはより永続的な解決策です。")]
        public bool ImmuneToDamage = false;

        /// キャラクターが攻撃を受けたときに再生するMMFeedbacks
        [Tooltip("キャラクターが攻撃を受けたときに再生するMMFeedbacks")]
        public MMFeedbacks DamageFeedbacks;

        /// trueの場合、致命的な攻撃かどうかに関係なくDamageFeedbackが再生されます
        [Tooltip("trueの場合、致命的な攻撃かどうかに関係なくDamageFeedbackが再生されます")]
        public bool TriggerDamageFeedbackOnDeath = true;

        /// trueの場合、ダメージ値がMMFeedbacksのIntensityパラメータとして渡され、ダメージが増加するにつれてより強烈なフィードバックをトリガーできます
        [Tooltip("trueの場合、ダメージ値がMMFeedbacksのIntensityパラメータとして渡され、ダメージが増加するにつれてより強烈なフィードバックをトリガーできます")]
        public bool FeedbackIsProportionalToDamage = false;

        /// ダメージを受けたときにスプライト（ある場合）を点滅させるかどうか？
        [Tooltip("ダメージを受けたときにスプライト（ある場合）を点滅させるかどうか？")]
        public bool FlickerSpriteOnHit = true;

        /// スプライトが点滅する色
        [Tooltip("スプライトが点滅する色")]
        [MMCondition("FlickerSpriteOnHit", true)]
        public Color FlickerColor = new Color32(255, 20, 20, 255);

        [MMInspectorGroup("ノックバック", true, 6)]

        /// このオブジェクトがノックバックを受けることができるかどうか
        [Tooltip("このオブジェクトがノックバックを受けることができるかどうか")]
        public bool ImmuneToKnockback = false;

        /// 受けたダメージがゼロの場合、このオブジェクトがダメージノックバックに免疫があるかどうか
        [Tooltip("受けたダメージがゼロの場合、このオブジェクトがダメージノックバックに免疫があるかどうか")]
        public bool ImmuneToKnockbackIfZeroDamage = false;

        [MMInspectorGroup("死亡", true, 7)]

        [MMInformation(
            "ここでは、オブジェクトが死亡したときに生成するエフェクト、適用する力（corgi controllerが必要）、ゲームスコアに追加するポイント数、およびキャラクターがリスポーンする場所（プレイヤーキャラクター以外のみ）を設定できます。",
            MoreMountains.Tools.MMInformationAttribute.InformationType.Info, false)]
        /// キャラクターが死亡したときに再生するMMFeedbacks
        [Tooltip("キャラクターが死亡したときに再生するMMFeedbacks")]
        public MMFeedbacks DeathFeedbacks;

        /// これがfalseでない場合、オブジェクトは死後もその場に残ります
        [Tooltip("これがfalseでない場合、オブジェクトは死後もその場に残ります")]
        public bool DestroyOnDeath = true;

        /// キャラクターが破壊または無効化されるまでの時間（秒）
        [Tooltip("キャラクターが破壊または無効化されるまでの時間（秒）")]
        public float DelayBeforeDestruction = 0f;

        /// trueの場合、キャラクターが死亡時にコリジョンがオフになります
        [Tooltip("trueの場合、キャラクターが死亡時にコリジョンがオフになります")]
        public bool CollisionsOffOnDeath = true;

        /// trueの場合、死亡時に重力がオフになります
        [Tooltip("trueの場合、死亡時に重力がオフになります")]
        public bool GravityOffOnDeath = false;

        /// オブジェクトのヘルスがゼロに達したときにプレイヤーが獲得するポイント
        [Tooltip("オブジェクトのヘルスがゼロに達したときにプレイヤーが獲得するポイント")]
        public int PointsWhenDestroyed;

        /// これがfalseに設定されている場合、キャラクターは死亡場所でリスポーンし、そうでなければ初期位置（シーン開始時）に移動されます
        [Tooltip(
            "これがfalseに設定されている場合、キャラクターは死亡場所でリスポーンし、そうでなければ初期位置（シーン開始時）に移動されます")]
        public bool RespawnAtInitialLocation = false;

        [MMInspectorGroup("死亡時の力", true, 10)]

        /// 死亡時に力を適用するかどうか
        [Tooltip("死亡時に力を適用するかどうか")]
        public bool ApplyDeathForce = true;

        /// キャラクターが死亡したときに適用される力
        [Tooltip("キャラクターが死亡したときに適用される力")]
        public Vector2 DeathForce = new Vector2(0, 10);

        /// 死亡時にコントローラーの力を0に設定するかどうか
        [Tooltip("死亡時にコントローラーの力を0に設定するかどうか")]
        public bool ResetForcesOnDeath = false;

        /// trueの場合、復活時に色がリセットされます
        [Tooltip("trueの場合、復活時に色がリセットされます")]
        public bool ResetColorOnRevive = true;
        /// レンダラーのシェーダーで色を定義するプロパティの名前 
        [Tooltip("レンダラーのシェーダーで色を定義するプロパティの名前")]
        [MMCondition("ResetColorOnRevive", true)]
        public string ColorMaterialPropertyName = "_Color";
        /// trueの場合、このコンポーネントはマテリアルのインスタンスで作業する代わりにマテリアルプロパティブロックを使用します。
        [Tooltip("trueの場合、このコンポーネントはマテリアルのインスタンスで作業する代わりにマテリアルプロパティブロックを使用します。")]
        public bool UseMaterialPropertyBlocks = false;

        [MMInspectorGroup("共有ヘルスとダメージ耐性", true, 11)]

        /// このHealthが影響を与えるCharacter、空の場合は同じゲームオブジェクトから選択します
        [Tooltip("このHealthが影響を与えるCharacter、空の場合は同じゲームオブジェクトから選択します")]
        public Character AssociatedCharacter;

        /// 別のHealthコンポーネント（通常は別のキャラクター上）、すべてのヘルスがリダイレクトされます
        [Tooltip("別のHealthコンポーネント（通常は別のキャラクター上）、すべてのヘルスがリダイレクトされます")]
        public Health MasterHealth;

        /// trueの場合、MasterHealthを使用時、このHealthはダメージを受けず、すべてのダメージがリダイレクトされます。falseの場合、このHealthは自身のHealthが消費されたときに死亡できます
        [Tooltip("trueの場合、MasterHealthを使用時、このHealthはダメージを受けず、すべてのダメージがリダイレクトされます。falseの場合、このHealthは自身のHealthが消費されたときに死亡できます")]
        public bool OnlyDamageMaster = true;

        /// trueの場合、MasterHealthを使用時、MasterHealthが死亡するとこのHealthも死亡します
        [Tooltip("trueの場合、MasterHealthを使用時、MasterHealthが死亡するとこのHealthも死亡します")]
        public bool KillOnMasterHealthDeath = false;

        /// このHealthがダメージを受けたときに処理に使用するDamageResistanceProcessor
        [Tooltip("このHealthがダメージを受けたときに処理に使用するDamageResistanceProcessor")]
        public DamageResistanceProcessor TargetDamageResistanceProcessor;

        public float LastDamage { get; set; }
        public Vector3 LastDamageDirection { get; set; }
        public bool Initialized => _initialized;
        public CorgiController AssociatedController => _controller;

        // リスポーン
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
        /// Awakeで、ヘルスを初期化します
        /// </summary>
        protected virtual void Start()
        {
            Initialization();
            InitializeSpriteColor();
            InitializeCurrentHealth();
        }

        /// <summary>
        /// 有用なコンポーネントを取得し、ダメージを有効にして初期色を取得します
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
        /// ヘルスを初期値または現在の値に初期化します
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
        /// キャラクタースプライトの初期色を保存します。
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
        /// 元のスプライト色を復元します
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
        /// このHealthコンポーネントがこのフレームでダメージを受けることができる場合はtrue、そうでなければfalseを返します
        /// </summary>
        /// <returns></returns>
        public virtual bool CanTakeDamageThisFrame()
        {
            // オブジェクトが無敵状態の場合、何もせずに終了します
            if ( Invulnerable || ImmuneToDamage )
            {
                return false;
            }

            if ( !this.enabled )
            {
                return false;
            }

            // すでにゼロ以下の場合、何もせずに終了します
            if ( (CurrentHealth <= 0) && (InitialHealth != 0) )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// オブジェクトがダメージを受けたときに呼び出されます
        /// </summary>
        /// <param name="damage">失われるヘルスポイントの量。</param>
        /// <param name="instigator">ダメージを引き起こしたオブジェクト。</param>
        /// <param name="flickerDuration">ダメージを受けた後にオブジェクトが点滅する時間（秒）。</param>
        /// <param name="invincibilityDuration">攻撃後の短い無敵時間の継続時間。</param>
        public virtual void Damage(float damage, GameObject instigator, float flickerDuration,
            float invincibilityDuration, Vector3 damageDirection, List<TypedDamage> typedDamages = null)
        {
            if ( !gameObject.activeInHierarchy )
            {
                return;
            }

            // オブジェクトが無敵状態の場合、何もせずに終了します
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

            // 状態変化を処理します
            ComputeCharacterConditionStateChanges(typedDamages);
            ComputeCharacterMovementMultipliers(typedDamages);

            if ( damage <= 0 )
            {
                OnHitZero?.Invoke();
                return;
            }

            // キャラクターのヘルスをダメージ分減少させます
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

            // キャラクターがProjectiles、Player、Enemiesと衝突することを防ぎます
            if ( (invincibilityDuration > 0) && gameObject.activeInHierarchy )
            {
                EnablePostDamageInvulnerability();
                StartCoroutine(DisablePostDamageInvulnerability(invincibilityDuration));
            }

            // ダメージを受けたイベントをトリガーします
            MMDamageTakenEvent.Trigger(this, instigator, CurrentHealth, damage, previousHealth);

            if ( _animator != null )
            {
                _animator.SetTrigger("Damage");
            }

            // ダメージフィードバックを再生します
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
                // キャラクターのスプライトを点滅させます
                if ( _renderer != null )
                {
                    StartCoroutine(MMImage.Flicker(_renderer, _initialColor, FlickerColor, 0.05f, flickerDuration));
                }
            }

            // ヘルスバーを更新します
            UpdateHealthBar(true);

            // ヘルスがゼロに達した場合、ヘルスをゼロに設定します（ヘルスバーに有用）
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
        /// ダメージを適用せず、OnHitZeroをトリガーします
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
        /// キャラクターを殺し、死亡エフェクトを生成し、ポイントを処理するなど
        /// </summary>
        public virtual void Kill()
        {
            if ( ImmuneToDamage )
            {
                return;
            }

            if ( _character != null )
            {
                // 死亡状態をtrueに設定します
                _character.ConditionState.ChangeState(CharacterStates.CharacterConditions.Dead);
                _character.Reset();

                if ( _character.CharacterType == Character.CharacterTypes.Player )
                {
                    CorgiEngineEvent.Trigger(CorgiEngineEventTypes.PlayerDeath, _character);
                }
            }
            SetHealth(0f, _thisObject);

            // さらなるダメージを防ぎます
            DamageDisabled();

            StopAllDamageOverTime();

            // 破壊エフェクトを生成します
            DeathFeedbacks?.PlayFeedbacks();

            // 必要に応じてポイントを追加します。
            if ( PointsWhenDestroyed != 0 )
            {
                // GameManagerがキャッチする新しいポイントイベントを送信します（およびそれを聞く他のクラスも）
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

            // コントローラーがある場合、衝突を削除し、リスポーンのためのパラメータを復元し、死亡力を適用します
            if ( _controller != null )
            {
                // 今後は衝突を無視させます
                if ( CollisionsOffOnDeath )
                {
                    _controller.CollisionsOff();
                    if ( _collider2D != null )
                    {
                        _collider2D.enabled = false;
                    }
                }

                // パラメータをリセットします
                _controller.ResetParameters();

                if ( GravityOffOnDeath )
                {
                    _controller.GravityActive(false);
                }

                // 必要に応じて死亡時にコントローラーの力をリセットします
                if ( ResetForcesOnDeath )
                {
                    _controller.SetForce(Vector2.zero);
                }

                // 死亡力を適用します
                if ( ApplyDeathForce )
                {
                    _controller.GravityActive(true);
                    _controller.SetForce(DeathForce);
                }
            }


            // キャラクターがある場合、その状態を変更したいと思います
            if ( _character != null )
            {
                // 死亡状態をtrueに設定します
                _character.ConditionState.ChangeState(CharacterStates.CharacterConditions.Dead);
                _character.Reset();

                // これがプレイヤーの場合、ここで終了します
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
                // 最後にオブジェクトを破壊します
                DestroyObject();
            }
        }

        /// <summary>
        /// このオブジェクトを復活させます。
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
        /// キャラクターの設定に応じて、オブジェクトを破壊するか、破壊を試みます
        /// </summary>
        protected virtual void DestroyObject()
        {
            if ( !DestroyOnDeath )
            {
                return;
            }

            if ( _autoRespawn == null )
            {
                // オブジェクトは、リスポーン時に復元できるように非アクティブになります
                gameObject.SetActive(false);
            }
            else
            {
                _autoRespawn.Kill();
            }
        }

        /// <summary>
        /// タイプに関係なく、すべての継続ダメージを中断します
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
        /// 中断不可能なものを含め、すべての継続ダメージを中断します（通常死亡時）
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
        /// 指定されたタイプのすべての継続ダメージを中断します
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
        /// 指定された回数の繰り返し（最初のダメージ適用を含む、インスペクターでの簡単な計算のため）と指定された間隔で継続ダメージを適用します。
        /// オプションで、ダメージが中断可能かどうかを決定できます。この場合、InterruptAllDamageOverTime()を呼び出すとこれらの適用が停止され、毒を治療する場合などに有用です。
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
        /// 継続ダメージを適用するために使用されるコルーチン
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
        /// 潜在的な耐性を処理した後、このヘルスが受けるべきダメージを返します
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
            // 潜在的な耐性を通してダメージを処理します
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
        /// 耐性を通して処理することで新しいノックバック力を決定します
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
        /// このHealthがノックバックを受けることができる場合はtrue、そうでなければfalseを返します
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
        /// 耐性を通して処理し、必要に応じて状態変化を適用します
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
        /// 耐性リストを通して処理し、必要に応じて移動倍率を適用します
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
        /// キャラクターがヘルスを取得したときに呼ばれます（例：スティムパックから）
        /// </summary>
        /// <param name="health">キャラクターが取得するヘルス。</param>
        /// <param name="instigator">キャラクターにヘルスを与えるもの。</param>
        public virtual void GetHealth(float health, GameObject instigator)
        {
            // この関数はキャラクターのHealthにヘルスを追加し、MaxHealthを超えることを防ぎます。
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
        /// キャラクターのヘルスをパラメータで指定されたものに設定します
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
        /// キャラクターのヘルスを最大値にリセットします
        /// </summary>
        public virtual void ResetHealthToMaxHealth()
        {
            CurrentHealth = MaximumHealth;
            UpdateHealthBar(false);
            HealthChangeEvent.Trigger(this, CurrentHealth);
        }

        /// <summary>
        /// キャラクターのヘルスバーの進行状況を更新します。
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
                    // ヘルスバーを更新します
                    if ( GUIManager.HasInstance )
                    {
                        GUIManager.Instance.UpdateHealthBar(CurrentHealth, 0f, MaximumHealth, _character.PlayerID);
                    }
                }
            }
        }

        /// <summary>
        /// キャラクターがダメージを受けることを防ぎます
        /// </summary>
        public virtual void DamageDisabled()
        {
            TemporarilyInvulnerable = true;
        }

        /// <summary>
        /// キャラクターがダメージを受けることを許可します
        /// </summary>
        public virtual void DamageEnabled()
        {
            TemporarilyInvulnerable = false;
        }

        /// <summary>
        /// キャラクターがダメージを受けることを防ぎます
        /// </summary>
        public virtual void EnablePostDamageInvulnerability()
        {
            PostDamageInvulnerable = true;
        }

        /// <summary>
        /// キャラクターがダメージを受けることを許可します
        /// </summary>
        public virtual void DisablePostDamageInvulnerability()
        {
            PostDamageInvulnerable = false;
        }

        /// <summary>
        /// キャラクターがダメージを受けることを許可します
        /// </summary>
        public virtual IEnumerator DisablePostDamageInvulnerability(float delay)
        {
            yield return MMCoroutine.WaitFor(delay);
            PostDamageInvulnerable = false;
        }

        /// <summary>
        /// 指定された遅延後にキャラクターが再びダメージを受けることができるようになります
        /// </summary>
        /// <returns>レイヤー衝突。</returns>
        public virtual IEnumerator DamageEnabled(float delay)
        {
            yield return MMCoroutine.WaitFor(delay);
            TemporarilyInvulnerable = false;
        }

        /// <summary>
        /// オブジェクトが有効化されたとき（例：リスポーン時）、初期ヘルスレベルを復元します
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
        /// 無効化時に実行中のすべてのinvokeをキャンセルします
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