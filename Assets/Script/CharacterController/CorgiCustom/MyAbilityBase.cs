using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// キャラクターのアビリティを処理するためのオーバーライド可能なベースクラス
    /// このクラスを継承して独自のアビリティを作成する
    /// </summary>
    /// 
    public class MyAbilityBase : CharacterAbility
    {
        #region 権限・制限設定

        /// <summary>
        /// アビリティの実行をブロックする武器状態の配列
        /// キャラクターの武器がこれらの状態の時にアビリティを発動しようとしても許可されない
        /// 例：攻撃中にアビリティを使用不可にする
        /// 
        /// 不要につき隠蔽
        /// </summary>
        [HideInInspector]
        [Tooltip("アビリティの実行をブロックする武器状態の配列。キャラクターの武器がこれらの状態の時にアビリティを発動しようとしても許可されない。例：攻撃中にアビリティを使用不可にする")]
        public new Weapon.WeaponStates[] BlockingWeaponStates;

        #endregion

        #region 内部変数・コンポーネント参照

        // キャラクター関連の参照
        protected new MyCharacter _character;                              // 親キャラクターの参照
        protected new MyHealth _health;                                    // ヘルス管理コンポーネント
        protected CharacterHorizontalMovement _characterHorizontalMovement; // 水平移動アビリティ
        protected new MyCorgiController _controller;                       // Corgiコントローラー

        // 状態管理
        protected new MyConditionStateMachine _condition; // コンディション状態マシン

        #endregion

        #region プロパティ

        /// <summary>
        /// アビリティが実行可能かどうかを判定するプロパティ
        /// 各種ブロッキング状態をチェックし、最終的にAbilityPermittedの値を返す
        /// </summary>
        public override bool AbilityAuthorized
        {
            get
            {
                // キャラがいないかアビリティが許可されてないなら早期リターン
                if ( !AbilityPermitted || _character == null )
                {
                    return false;
                }

                // 移動状態のブロックチェック
                if ( ((uint)(_movement.CurrentState) & _blockingMovementBit) > 0 )
                {
                    return false;
                }

                // コンディション状態のブロックチェック
                if ( (((uint)_condition.CurrentState) & _blockingConditionBit) > 0 )
                {
                    return false;
                }

                // すべてのチェックを通過した場合はAbilityPermittedの値を返す
                return true;
            }
        }

        #endregion

        #region 内部変数・コンポーネント参照

        /// <summary>
        /// アビリティ禁止状態を保持するためのビットフラグ
        /// </summary>
        private uint _blockingConditionBit = 0;

        /// <summary>
        /// アビリティ禁止行動状態を保持するためのビットフラグ
        /// </summary>
        private uint _blockingMovementBit = 0;

        #endregion

        #region 初期化・セットアップ

        /// <summary>
        /// 必要なコンポーネントを取得・保存して初期化を行う
        /// このメソッドで各種参照を設定し、アビリティを使用可能な状態にする
        /// 
        /// これに関しては必ずbaseを実行する
        /// </summary>
        protected override void Initialization()
        {
            // 親オブジェクトから各種コンポーネントを取得
            _character = this.gameObject.GetComponentInParent<MyCharacter>();
            _controller = this.gameObject.GetComponentInParent<MyCorgiController>();

            // キャラクターから関連アビリティを取得
            _characterHorizontalMovement = _character.FindAbility<CharacterHorizontalMovement>();
            _characterGravity = _character.FindAbility<CharacterGravity>();
            _health = _character.CharacterHealth;

            // アニメーターの設定
            BindAnimator();

            // キャラクターが存在する場合、各種参照を設定
            if ( _character != null )
            {
                _characterTransform = _character.transform;
                _sceneCamera = _character.SceneCamera;
                _inputManager = _character.LinkedInputManager;
                _state = _character.CharacterState;
                _movement = _character.MovementState;
                _condition = _character.ConditionState;
            }

            //無効状態判断用のbitを初期化
            for ( int i = 0; i < BlockingConditionStates.Length; i++ )
            {
                _blockingConditionBit &= (uint)(1 << (int)BlockingConditionStates[i]);
            }

            //無効行動判断用のbitを初期化
            for ( int i = 0; i < BlockingMovementStates.Length; i++ )
            {
                _blockingMovementBit &= (uint)(1 << (int)BlockingMovementStates[i]);
            }

            // 初期化完了フラグを設定
            _abilityInitialized = true;
        }

        #endregion

        #region 入力処理


        #endregion

        #region アビリティ処理フェーズ

        /// <summary>
        /// アビリティの3つのパスの最初のパス
        /// EarlyUpdate()のようなものと考える。主に入力処理を行う
        /// </summary>
        //public virtual void EarlyProcessAbility()
        //{
        //    InternalHandleInput();
        //}

        /// <summary>
        /// アビリティの3つのパスの2番目のパス
        /// Update()のようなものと考える。メインの処理を行う
        /// </summary>
        //public virtual void ProcessAbility()
        //{
        //    // 継承先で実装
        //}

        /// <summary>
        /// アビリティの3つのパスの最後のパス
        /// LateUpdate()のようなものと考える。後処理を行う
        /// </summary>
        //public virtual void LateProcessAbility()
        //{
        //    // 継承先で実装
        //}

        /// <summary>
        /// キャラクターのアニメーターにパラメーターを送信するためにオーバーライドする
        /// Early、通常、Lateの各process()の後に、Characterクラスによって1サイクルに1回呼ばれる
        /// </summary>
        //public virtual void UpdateAnimator()
        //{
        //    // 継承先で実装
        //}

        #endregion

        #region アビリティ制御メソッド

        /// <summary>
        /// アビリティの許可状態を変更する
        /// </summary>
        /// <param name="abilityPermitted">trueの場合アビリティを許可</param>
        //public virtual void PermitAbility(bool abilityPermitted)
        //{
        //    AbilityPermitted = abilityPermitted;
        //}

        /// <summary>
        /// キャラクターが反転した時にこのアビリティで何が起こるかを指定するためにオーバーライドする
        /// </summary>
        //public virtual void Flip()
        //{
        //    // 継承先で実装
        //}

        /// <summary>
        /// このアビリティのパラメーターをリセットするためにオーバーライドする
        /// キャラクターが倒されたとき、リスポーンの準備として自動的に呼ばれる
        /// </summary>
        //public virtual void ResetAbility()
        //{
        //    // 継承先で実装
        //}

        #endregion

        #region イベントハンドラー

        /// <summary>
        /// キャラクターがリスポーンした時にこのアビリティに何が起こるかを記述するためにオーバーライドする
        /// 
        /// OnEnable()イベントに近い
        /// </summary>
        //protected virtual void OnRespawn()
        //{
        //    // 継承先で実装
        //}

        /// <summary>
        /// キャラクターが死亡した時にこのアビリティに何が起こるかを記述するためにオーバーライドする
        /// デフォルトでは開始フィードバックを停止する
        /// 
        /// Disenable()イベントに近い
        /// </summary>
        //protected virtual void OnDeath()
        //{
        //    StopStartFeedbacks();
        //}

        /// <summary>
        /// キャラクターがヒットを受けた時にこのアビリティに何が起こるかを記述するためにオーバーライドする
        /// 攻撃受けた時のイベント
        /// </summary>
        //protected virtual void OnHit()
        //{
        //    // 継承先で実装
        //}

        /// <summary>
        /// 攻撃をヒットさせたときDamageOnTouchから発火するイベント
        /// </summary>  
        public virtual void OnAttack(MyCharacter hitCharacter)
        {

        }

        /// <summary>
        /// 攻撃がヒットしたときHealthから発火するイベント
        /// </summary>  
        public virtual void OnDamage(MyCharacter attacker)
        {

        }

        #endregion

    }
}