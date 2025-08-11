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
    /// このクラスはキャラクターのCorgiControllerコンポーネントを操縦します。
    /// ここにキャラクターのゲームルール（ジャンプ、ダッシュ、射撃など）をすべて実装します。
    /// アニメーターパラメーター: Grounded (bool), xSpeed (float), ySpeed (float), 
    /// CollidingLeft (bool), CollidingRight (bool), CollidingBelow (bool), CollidingAbove (bool), Idle (bool)
    /// Random : 毎フレーム更新される0～1のランダム値。状態遷移にバリエーションを追加するのに便利
    /// RandomConstant : Start時に生成される0～1000のランダムint値。このアニメーターの生存期間中は定数として保持される。
    /// 同じタイプのキャラクターが異なる行動を取るようにするのに便利
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Core/MyCharacter")]
    public class MyCharacter : Character
    {
        #region 定義

        #region enum定義

        /// <summary>
        /// 判断結果をまとめて格納するビット演算用
        /// </summary>
        [Flags]
        public enum JudgeResult : byte
        {
            何もなし = 0,
            モード変更した = 1 << 1,// この時は移動方向も変える
            ターゲット変更した = 1 << 2,
            行動を変更した = 1 << 3,
            方向を変更した = 1 << 4,
        }

        #endregion enum定義

        #region 構造体定義

        /// <summary>
        /// 行動に使用するデータの構造体。
        /// 現在の行動状態、移動方向、判断基準、など必要なものは全て収める。
        /// これに従って動くというデータ。
        /// 28Byte
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct MovementInfo
        {

            // これキャラがどう動くか、みたいなデータまで入れてる
            // 新規判断後はターゲット入れ替えとか、判断時間入れ替えとかちょっとキャラデータをいじる。

            /// <summary>
            /// ターゲットのハッシュコード
            /// これで相手を取得する。
            /// 味方への支援ムーブのターゲットもありうることは頭に入れる。
            /// </summary>
            public int targetHash;

            /// <summary>
            /// 現在のターゲットとの距離。マイナスもあるので方向でもある。
            /// </summary>
            public float targetDistance;

            /// <summary>
            /// 番号で行動を指定する。
            /// 攻撃に限らず逃走とかも全部。移動方向から使用モーション、行動の種別まで（魔法とか移動とか）
            /// こっちの構造データはステータスに持たせとこ。行動状態ごとに番号で指定された行動をする。
            /// 状態変更の場合、これで変更先の状態を指定する。
            /// </summary>
            public byte actNum;

            /// <summary>
            /// 変更先のモード。
            /// </summary>
            public byte changeMode;

            /// <summary>
            /// 判断結果についての情報を格納するビット
            /// </summary>
            public JudgeResult result;

            /// <summary>
            /// デバッグ用。
            /// 選択した行動条件を設定する。
            /// </summary>
            public byte selectActCondition;

            /// <summary>
            /// デバッグ用。
            /// 選択したターゲット選択条件を設定する。
            /// </summary>
            public byte selectTargetCondition;

            /// <summary>
            /// 新規判断時の処理
            /// 行動終了後？
            /// </summary>
            public void JudgeUpdate(int hashCode)
            {
                // 判断情報をキャラデータに反映する。
                // 時間に関してはゲームマネージャー実装後にマネージャーからとるように変更するよ。
                AIManager.instance.characterDataDictionary.UpdateDataAfterJudge(hashCode, actNum, result, 0);
            }

            /// <summary>
            /// 
            /// </summary>
            public string GetDebugData()
            {
                return $"{this.selectActCondition}番目の条件、{(TargetSelectCondition)this.selectTargetCondition}({this.selectTargetCondition})で判断";
            }

        }

        #endregion

        #endregion 定義

        #region フィールド

        [Header("ヘルス")]
        /// このキャラクターに関連付けられたHealthスクリプト、空の場合は自動的に取得されます
        [Tooltip("このキャラクターに関連付けられたHealthスクリプト、空の場合は自動的に取得されます")]
        public new MyHealth CharacterHealth;

        /// <summary>
        /// コンディション状態マシン
        /// </summary>
        [HideInInspector]
        public new MyConditionStateMachine ConditionState;

        /// <summary>
        /// 一時的なコンディション変更を処理するための停止トークン
        /// </summary>
        private CancellationTokenSource _conditionChangeCancellationTokenSource;

        protected new MyCorgiController _controller;

        /// テストで使用するステータス。<br></br>
        /// 判断間隔のデータが入っている。<br></br>
        /// インスペクタから設定。
        /// </summary>
        [SerializeField]
        protected BrainStatus status;

        /// <summary>
        /// 自己定義アビリティのキャッシュ
        /// </summary>
        protected new MyAbilityBase[] _characterAbilities;

        /// <summary>
        /// 何回判断したかを数える。<br></br>
        /// 非同期と同期で、期待する判断回数との間の誤差が異なるかを見る。<br></br>
        /// 最初の行動の分だけ1引いた初期値に。
        /// </summary>
        [HideInInspector]
        public long judgeCount = -1;

        /// <summary>
        /// ゲームオブジェクトのハッシュ値
        /// </summary>
        [HideInInspector]
        public int myHash;

        #endregion フィールド

        #region プロパティ


        /// <summary>
        /// キャラクターの様々な状態
        /// 実体がなく不要なので隠蔽
        /// </summary>
        private new CharacterStates CharacterState { get; set; }

        /// <summary>
        /// 自分の位置を返すプロパティ
        /// </summary>
        public float2 Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AIManager.instance.characterDataDictionary.GetPosition(myHash);
        }

        /// <summary>
        /// このキャラの現在のターゲットを取得するプロパティ
        /// </summary>
        public int TargetHash
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AIManager.instance.characterDataDictionary.GetTargetHash(myHash);
        }


        /// <summary>
        /// このキャラの現在のターゲットを取得するプロパティ
        /// </summary>
        public MyCorgiController Controller
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _controller;
        }

        #endregion プロパティ

        #region コーギーコントローラーメソッド

        /// <summary>
        /// 入力マネージャー、カメラ、コンポーネントを取得・保存します
        /// </summary>
        public override void Initialization()
        {
            // ステートマシンを初期化
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

            // カメラターゲットをインスタンス化
            if ( CameraTarget == null )
            {
                CameraTarget = new GameObject();
                CameraTarget.transform.SetParent(this.transform);
                CameraTarget.transform.localPosition = Vector3.zero;
                CameraTarget.name = "CameraTarget";
            }
            _cameraTargetInitialPosition = CameraTarget.transform.localPosition;

            // 現在の入力マネージャーを取得
            SetInputManager();
            GetMainCamera();

            // コンポーネントを将来の使用のために保存
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

            // 新しいキャラデータを送り、コンバットマネージャーに送る。
            // いや、やっぱり材料送って向こうで作ってもらおう。
            // NativeContainer含む構造体をコピーするのなんかこわい。
            // ただもしコピーしても、こっちで作った分はローカル変数でしかないからDispose()周りの問題はないはず。
            AIManager.instance.CharacterAdd(this.status, this);
        }

        /// <summary>
        /// 毎フレーム実行します。より柔軟性を持たせるためにUpdateから分離されています。
        /// </summary>
        protected override void EveryFrame()
        {
            HandleCharacterStatus();

            // アビリティを処理
            EarlyProcessAbilities();

            if ( Time.timeScale != 0f )
            {
                ProcessAbilities();
                LateProcessAbilities();

                // カメラターゲットを更新する処理は不要
                // proCamera 2d使うので
                //HandleCameraTarget();
            }

            // 各種状態をアニメーターに送信
            UpdateAnimators();
            RotateModel();
        }

        /// <summary>
        /// アビリティを取得し、将来の使用のためにキャッシュします
        /// 実行時にアビリティを追加する場合は、このメソッドを必ず呼び出してください
        /// 理想的には、実行時にコンポーネントを追加することは避けたいものです。コストがかかるからです。
        /// 代わりにコンポーネントを有効化/無効化することをお勧めします。
        /// しかし、必要な場合は、このメソッドを呼び出してください。
        /// </summary>
        public override void CacheAbilities()
        {
            // 自分のレベルでアビリティをすべて取得
            _characterAbilities = this.gameObject.GetComponents<MyAbilityBase>();

            // ユーザーがより多くのノードを指定している場合
            if ( (AdditionalAbilityNodes != null) && (AdditionalAbilityNodes.Count > 0) )
            {
                // 一時リストを作成
                List<MyAbilityBase> tempAbilityList = new List<MyAbilityBase>();

                // すでに見つけたアビリティをすべてリストに入れる
                for ( int i = 0; i < _characterAbilities.Length; i++ )
                {
                    tempAbilityList.Add(_characterAbilities[i]);
                }

                // ノードからのものを追加
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
        /// 登録されているすべてのアビリティのEarly Processメソッドを呼び出します
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
        /// 登録されているすべてのアビリティのProcessメソッドを呼び出します
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
        /// 登録されているすべてのアビリティのLate Processメソッドを呼び出します
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
        /// アニメーターパラメーターを初期化します。
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

            // 定数フロートアニメーションパラメーターを更新
            int randomConstant = UnityEngine.Random.Range(0, 1000);
            MMAnimatorExtensions.UpdateAnimatorInteger(_animator, _randomConstantAnimationParameter, randomConstant, _animatorParameters);
        }

        /// <summary>
        /// Update()で呼び出され、各アニメーターパラメーターを対応するState値に設定します
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
        /// キャラクターの状態を処理します。
        /// </summary>
        protected override void HandleCharacterStatus()
        {
            // キャラクターが死んでいる場合、水平移動を防ぐ
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

            // キャラクターが凍結している場合、移動を防ぐ
            if ( ConditionState.CurrentState == CharacterStates.CharacterConditions.Frozen )
            {
                _controller.GravityActive(false);
                _controller.SetForce(Vector2.zero);
            }
        }

        /// <summary>
        /// このキャラクターを凍結します。
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
        /// このキャラクターの凍結を解除します
        /// </summary>
        public virtual void UnFreeze()
        {
            _controller.GravityActive(true);
            ConditionState.ChangeState(_conditionStateBeforeFreeze);
        }

        /// <summary>
        /// プレイヤーを無効にするために呼び出されます（例えばレベルの終わりに）。
        /// これ以降、移動や入力への応答はしません。
        /// </summary>
        public virtual void Disable()
        {
            enabled = false;
            _controller.enabled = false;
            this.gameObject.MMGetComponentNoAlloc<Collider2D>().enabled = false;
        }

        /// <summary>
        /// パラメーターで渡された場所でプレイヤーをリスポーンさせます
        /// </summary>
        /// <param name="spawnPoint">リスポーンの場所</param>
        public virtual void RespawnAt(Transform spawnPoint, FacingDirections facingDirection)
        {
            if ( !gameObject.activeInHierarchy )
            {
                //Debug.LogError("Spawn : your Character's gameobject is inactive");
                return;
            }

            UnFreeze();

            // キャラクターが正しい方向を向いていることを確認
            Face(facingDirection);

            // 死から蘇らせる（死んでいた場合）
            ConditionState.ChangeState(CharacterStates.CharacterConditions.Normal);
            // 2Dコライダーを再有効化
            this.gameObject.MMGetComponentNoAlloc<Collider2D>().enabled = true;
            // 再びコリジョンを処理させる
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
        /// キャラクターとその依存関係（ジェットパックなど）を水平に反転します
        /// </summary>
        public virtual void Flip(bool IgnoreFlipOnDirectionChange = false)
        {
            // キャラクターを反転させたくない場合は、何もせずに終了
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
                    // スプライトレンダラーベースの場合、flipX属性を反転
                    if ( _spriteRenderer != null )
                    {
                        _spriteRenderer.flipX = !_spriteRenderer.flipX;
                    }
                }
            }

            // キャラクターを水平に反転
            FlipModel();

            if ( _animator != null )
            {
                MMAnimatorExtensions.SetAnimatorTrigger(_animator, _flipAnimationParameter, _animatorParameters, PerformAnimatorSanityChecks);
            }

            IsFacingRight = !IsFacingRight;

            // すべてのアビリティに反転することを伝える
            foreach ( MyAbilityBase ability in _characterAbilities )
            {
                if ( ability.enabled )
                {
                    ability.Flip();
                }
            }
        }

        /// <summary>
        /// 指定した期間キャラクターのコンディションを変更し、その後リセットするために使用します。
        /// しばらくの間重力を無効にし、オプションで力もリセットできます。
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
        /// ChangeCharacterConditionTemporarilyによる一時的なコンディション変更を処理するUnitaskVoidメソッド。
        /// キャンセルトークン使ってるので多用しない方がいいかも。
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

        #region イベント

        /// <summary>
        /// キャラクターが死亡したときに呼び出されます。
        /// すべてのアビリティのReset()メソッドを呼び出すので、必要に応じて設定を元の値に復元できます
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
        /// 蘇生時に、スポーン方向を強制します
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
        /// キャラクター死亡時に、ブレインとダメージオンタッチエリアを無効にします
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
        /// OnEnable時に、OnReviveイベントを登録します
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
        /// OnDisable時に、OnReviveイベントの登録を解除します
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
        /// 攻撃ヒット時に呼び出すイベント
        /// 呼び出し頻度が高いイベントはパフォーマンスのためデリゲートを使わない
        /// </summary>
        public virtual void OnAttack(MyCharacter hitCharacter)
        {
            foreach ( var ability in _characterAbilities )
            {
                ability.OnAttack(hitCharacter);
            }
        }

        /// <summary>
        /// 被弾時に呼び出すイベント
        /// 呼び出し頻度が高いイベントはパフォーマンスのためデリゲートを使わない
        /// </summary>
        public virtual void OnDamage(MyCharacter attacker)
        {
            foreach ( var ability in _characterAbilities )
            {
                ability.OnDamage(attacker);
            }
        }

        #endregion イベント

        #endregion コーギーコントローラーメソッド

        #region キャラコントローラーメソッド

        /// <summary>
        /// 行動を判断するメソッド。
        /// </summary>
        protected void MoveJudgeAct()
        {
            // 50%の確率で左右移動の方向が変わる。
            // moveDirection = (UnityEngine.Random.Range(0, 100) >= 50) ? 1 : -1;

            //  rb.linearVelocityX = moveDirection * status.xSpeed;

            //Debug.Log($"数値：{moveDirection * status.xSpeed} 速度：{rb.linearVelocityX}");

            //lastJudge = GameManager.instance.NowTime;
            this.judgeCount++;
        }

        /// <summary>
        /// ターゲットを決めて攻撃する。
        /// </summary>
        protected void Attackct()
        {

        }

        /// <summary>
        /// 近くを範囲探査して敵情報を取得する処理のひな型
        /// ここで範囲内に取得したキャラを10体までバッファして距離順でソート
        /// ひとまず下書き
        /// </summary>
        public void NearSearch()
        {
            unsafe
            {
                // スタックにバッファを確保（10体まで）
                Span<RaycastHit2D> results = stackalloc RaycastHit2D[10];

                //int hitCount = Physics.SphereCastNonAlloc(
                //    AIManager.instance.charaDataDictionary[objecthash].liveData.nowPosition,
                //    20,
                //    results,
                //    0
                //);

            }

        }


        #endregion キャラコントローラーメソッド

    }
}