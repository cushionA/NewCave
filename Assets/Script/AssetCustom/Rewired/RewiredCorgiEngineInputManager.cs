namespace Rewired.Integration.CorgiEngine
{
    using UnityEngine;
    using MoreMountains.CorgiEngine;
    using MoreMountains.Tools;
    using System.Collections.Generic;
    using System;
    using System.Collections;
    using Cysharp.Threading.Tasks;

    /// <summary>
    /// この永続的なシングルトンは、入力を処理し、プレーヤーにコマンドを送信します。
    /// 重要：このスクリプトの実行順序は-100でなければならない。
    /// スクリプトの実行順序は、スクリプトのファイルをクリックし、スクリプトのインスペクタの右下にある実行順序ボタンをクリックすることで定義できます。
    /// See https://docs.unity3d.com/Manual/class-ScriptExecution.html for more details
    /// </summary>
    [AddComponentMenu("Corgi Engine/Managers/Rewired Input Manager")]
    public class RewiredCorgiEngineInputManager : InputManager
    {

        #region Rewired用の変数

        private const string REWIRED_SYSTEM_PAUSE_ACTION_NAME = "SystemPause";
        private Rewired.Player _rewiredPlayer;
        private int _rewiredActionId_horizontal;
        private int _rewiredActionId_vertical;

        private int _rewiredActionId_siteHorizontal;
        private int _rewiredActionId_siteVertical;
        private int _rewiredActionId_UIHorizontal;
        private int _rewiredActionId_UIVertical;

        private int[] _rewiredButtonIds;

        private int _rewiredSystemPauseButtonId;

        #endregion

        /// <summary>
        /// 追加軸の名前
        /// </summary>
        protected string SiteHorizontal;
        protected string SiteVertical;
        protected string UIHorizontal;
        protected string UIVertical;
        protected Vector2 _siteMovement = Vector2.zero;
        protected Vector2 _UIMovement = Vector2.zero;

        /// <summary>
        /// これが真なら有効なボタンが入力されてる。
        /// </summary>
        bool _inputCheck;

        public Vector2 UIMovement { get { return _UIMovement; } }

        public Vector2 SiteMovement { get { return _siteMovement; } }

        #region 入力で使用するプロパティ

        public MMInput.IMButton sAttackButton { get; protected set; }
        public MMInput.IMButton bAttackButton { get; protected set; }
        public MMInput.IMButton ArtsButton { get; protected set; }

        public MMInput.IMButton CombinationButton { get; protected set; }

        public MMInput.IMButton AvoidButton { get; protected set; }
        public MMInput.IMButton GuardButton { get; protected set; }

        public MMInput.IMButton WeaponChangeButton { get; protected set; }

        public MMInput.IMButton MenuCallButton { get; protected set; }

        public MMInput.IMButton TipsButton { get; protected set; }

        public MMInput.IMButton SubmitButton { get; protected set; }
        public MMInput.IMButton CancelButton { get; protected set; }

        #endregion


        /// <summary>
        /// スタート時に使用するモードを探し、軸とボタンを初期化します。
        /// </summary>
        protected override void Start()
        {
            base.Start();

            if ( !ReInput.isReady )
            {
                Debug.LogError("Rewired: Rewired was not initialized. Setup could not be performed. A Rewired Input Manager must be in the scene and enabled. Falling back to default input handler.");
                return;
            }

            // Get the Rewired Id based on the PlayerID string
            _rewiredPlayer = ReInput.players.GetPlayer(PlayerID);
            if ( _rewiredPlayer == null )
            {
                Debug.LogError("Rewired: No Rewired Player was found for the PlayerID string \"" + PlayerID + "\". Falling back to default input handler.");
                return;
            }
        }

        /// <summary>
        /// ボタンを初期化します。ボタンを増やしたい場合は、必ずベースとなるInputManagerクラスのInintializeButtonsメソッドに登録してください。
        /// </summary>
        protected override void InitializeButtons()
        {
            ButtonList = new List<MMInput.IMButton>();
            ButtonList.Add(JumpButton = new MMInput.IMButton(null, "Jump", JumpButtonDown, JumpButtonPressed, JumpButtonUp));
            ButtonList.Add(InteractButton = new MMInput.IMButton(null, "Interact", InteractButtonDown, InteractButtonPressed, InteractButtonUp));
            ButtonList.Add(CancelButton = new MMInput.IMButton(null, "Cancel", CancelButtonDown, CancelButtonPressed, CancelButtonUp));
            ButtonList.Add(SubmitButton = new MMInput.IMButton(null, "Submit", SubmitButtonDown, SubmitButtonPressed, SubmitButtonUp));
            ButtonList.Add(TipsButton = new MMInput.IMButton(null, "TipsOn", TipsButtonDown, TipsButtonPressed, TipsButtonUp));
            ButtonList.Add(MenuCallButton = new MMInput.IMButton(null, "_Menu", MenuCallButtonDown, MenuCallButtonPressed, MenuCallButtonUp));
            ButtonList.Add(CombinationButton = new MMInput.IMButton(null, "Combination", CombinationButtonDown, CombinationButtonPressed, CombinationButtonUp));
            ButtonList.Add(sAttackButton = new MMInput.IMButton(null, "Fire1", sAttackButtonDown, sAttackButtonPressed, sAttackButtonUp));
            ButtonList.Add(bAttackButton = new MMInput.IMButton(null, "Fire2", bAttackButtonDown, bAttackButtonPressed, bAttackButtonUp));
            ButtonList.Add(ArtsButton = new MMInput.IMButton(null, "Arts", ArtsButtonDown, ArtsButtonPressed, ArtsButtonUp));
            ButtonList.Add(AvoidButton = new MMInput.IMButton(null, "Avoid", AvoidButtonDown, AvoidButtonPressed, AvoidButtonUp));
            ButtonList.Add(WeaponChangeButton = new MMInput.IMButton(null, "WeponHandChange", WeaponChangeButtonDown, WeaponChangeButtonPressed, WeaponChangeButtonUp));
            ButtonList.Add(GuardButton = new MMInput.IMButton(null, "Guard", GuardButtonDown, GuardButtonPressed, GuardButtonUp));


            if ( !ReInput.isReady )
                return;

            // Rewired Action Id を文字列ではなく整数値でキャッシュすることで高速化を図りました。
            _rewiredButtonIds = new int[ButtonList.Count];
            for ( int i = 0; i < _rewiredButtonIds.Length; i++ )
                _rewiredButtonIds[i] = -1; // init to invalid
            for ( int i = 0; i < _rewiredButtonIds.Length; i++ )
            {
                string actionName = StripPlayerIdFromActionName(ButtonList[i].ButtonID);
                if ( string.IsNullOrEmpty(actionName) )
                    continue;
                _rewiredButtonIds[i] = ReInput.mapping.GetActionId(actionName);
                //      Debug.Log($"test{actionName}{_rewiredButtonIds[i]}");
                // Find the Shoot action so we can reuse it instead of ShootAxis
                //ShootAxisの代わりにShootアクションを再利用できるように検索します。

            }
            _rewiredSystemPauseButtonId = ReInput.mapping.GetActionId(REWIRED_SYSTEM_PAUSE_ACTION_NAME);
        }

        #region 新規定義ボタンのためのメソッド。ボタンの状態を変化させる

        public virtual void GuardButtonDown() { GuardButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void GuardButtonPressed() { GuardButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void GuardButtonUp() { GuardButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }
        public virtual void sAttackButtonDown() { sAttackButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void sAttackButtonPressed() { sAttackButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void sAttackButtonUp() { sAttackButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void bAttackButtonDown() { bAttackButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void bAttackButtonPressed() { bAttackButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void bAttackButtonUp() { bAttackButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void ArtsButtonDown() { ArtsButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void ArtsButtonPressed() { ArtsButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void ArtsButtonUp() { ArtsButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void CombinationButtonDown() { CombinationButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void CombinationButtonPressed() { CombinationButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void CombinationButtonUp() { CombinationButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void AvoidButtonDown() { AvoidButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void AvoidButtonPressed() { AvoidButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void AvoidButtonUp() { AvoidButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }


        public virtual void WeaponChangeButtonDown() { WeaponChangeButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void WeaponChangeButtonPressed() { WeaponChangeButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void WeaponChangeButtonUp() { WeaponChangeButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }


        public virtual void MenuCallButtonDown() { MenuCallButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void MenuCallButtonPressed() { MenuCallButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void MenuCallButtonUp() { MenuCallButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }


        public virtual void TipsButtonDown() { TipsButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void TipsButtonPressed() { TipsButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void TipsButtonUp() { TipsButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void SubmitButtonDown() { SubmitButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void SubmitButtonPressed() { SubmitButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void SubmitButtonUp() { SubmitButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void CancelButtonDown() { CancelButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }
        public virtual void CancelButtonPressed() { CancelButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }
        public virtual void CancelButtonUp() { CancelButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        #endregion

        /// <summary>
        /// 軸のIDを初期化します。
        /// </summary>
        protected override void InitializeAxis()
        {
            _axisHorizontal = "MoveHorizontal";
            _axisVertical = "MoveVertical";
            SiteVertical = "SiteVertical";
            SiteHorizontal = "SiteHorizontal";
            UIVertical = "UIVertical";
            UIHorizontal = "UIHorizontal";

            if ( !ReInput.isReady )
                return;

            // Cache the Rewired Action Id integers instead of using strings for speed
            _rewiredActionId_horizontal = ReInput.mapping.GetActionId(StripPlayerIdFromActionName(_axisHorizontal));
            _rewiredActionId_vertical = ReInput.mapping.GetActionId(StripPlayerIdFromActionName(_axisVertical));
            _rewiredActionId_siteHorizontal = ReInput.mapping.GetActionId(StripPlayerIdFromActionName(SiteHorizontal));
            _rewiredActionId_siteVertical = ReInput.mapping.GetActionId(StripPlayerIdFromActionName(SiteVertical));
            _rewiredActionId_UIVertical = ReInput.mapping.GetActionId(StripPlayerIdFromActionName(UIVertical));
            _rewiredActionId_UIHorizontal = ReInput.mapping.GetActionId(StripPlayerIdFromActionName(UIHorizontal));
        }

        /// <summary>
	    /// アップデート時には、各種コマンドを確認し、それに応じて値や状態を更新します。
	    /// </summary>
	    protected override void Update()
        {
            SetMovement();

            // if ( MainUICon.instance.UIOn )
            //  {
            //  SetUIMovement();
            //  }
            GetInputButtons();

        }

        /// <summary>
        /// _inputCheckを初期化する
        /// </summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();
            _inputCheck = false;
        }

        /// <summary>
        /// 入力の変化を監視し、それに応じてボタンの状態を更新する。
        /// </summary>
        protected override void GetInputButtons()
        {
            //ボタンが一個ずつ押されてるか確認中
            for ( int i = 0; i < _rewiredButtonIds.Length; i++ )
            {
                if ( _rewiredPlayer.GetButton(_rewiredButtonIds[i]) )
                {

                    //Debug.Log("push");
                    ButtonList[i].TriggerButtonPressed();
                    _inputCheck = true;
                }
                if ( _rewiredPlayer.GetButtonDown(_rewiredButtonIds[i]) )
                {
                    ButtonList[i].TriggerButtonDown();
                    _inputCheck = true;
                }
                if ( _rewiredPlayer.GetButtonUp(_rewiredButtonIds[i]) )
                {
                    ButtonList[i].TriggerButtonUp();
                }
            }

            // Special handling for System Pause
            // Allow the System Player to trigger Pause on all players so the key
            // only has to be mapped to one fixed key and the assignment can be protected.
            Rewired.Player systemPlayer = ReInput.players.GetSystemPlayer();
            if ( systemPlayer.GetButtonDown(_rewiredSystemPauseButtonId) )
            {
                PauseButton.TriggerButtonDown();
            }
            if ( systemPlayer.GetButtonUp(_rewiredSystemPauseButtonId) )
            {
                PauseButton.TriggerButtonUp();
            }
        }

        /// <summary>
		/// LateUpdate()で呼び出され、登録された全てのボタンの状態を処理する。
		/// </summary>
		public override void ProcessButtonStates()
        {
            Span<MMInput.IMButton> buttonSpan = ButtonList.AsSpan();

            // for each button, if we were at ButtonDown this frame, we go to ButtonPressed. If we were at ButtonUp, we're now Off
            foreach ( MMInput.IMButton button in buttonSpan )
            {
                if ( button.State.CurrentState == MMInput.ButtonStates.ButtonDown )
                {
                    if ( DelayedButtonPresses )
                    {
                        DelayButtonState(button, MMInput.ButtonStates.ButtonPressed).Forget();
                    }
                    else
                    {
                        button.State.ChangeState(MMInput.ButtonStates.ButtonPressed);
                    }

                }
                if ( button.State.CurrentState == MMInput.ButtonStates.ButtonUp )
                {
                    if ( DelayedButtonPresses )
                    {
                        DelayButtonState(button, MMInput.ButtonStates.Off).Forget();
                    }
                    else
                    {
                        button.State.ChangeState(MMInput.ButtonStates.Off);
                    }
                }
            }

            // Update the ShootAxis state which is separate from other buttons
            if ( ShootAxis == MMInput.ButtonStates.ButtonDown )
            {
                ShootAxis = MMInput.ButtonStates.ButtonPressed;
            }
            if ( ShootAxis == MMInput.ButtonStates.ButtonUp )
            {
                ShootAxis = MMInput.ButtonStates.Off;
            }
        }

        /// <summary>
        /// 状態切り替えコルーチンのUnitask版
        /// </summary>
        /// <param name="button"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        async UniTask DelayButtonState(MMInput.IMButton button, MMInput.ButtonStates state)
        {
            await UniTask.DelayFrame(1);
            button.State.ChangeState(state);
        }

        /// <summary>
        /// Called every frame, gets primary movement values from Rewired Player
        /// </summary>
        public override void SetMovement()
        {
            if ( SmoothMovement )
            {

                _primaryMovement.x = _rewiredPlayer.GetAxis(_rewiredActionId_horizontal);
                _primaryMovement.y = _rewiredPlayer.GetAxis(_rewiredActionId_vertical);
            }
            else
            {

                _primaryMovement.x = _rewiredPlayer.GetAxisRaw(_rewiredActionId_horizontal);
                _primaryMovement.y = _rewiredPlayer.GetAxisRaw(_rewiredActionId_vertical);

            }
            //if(_primaryMovement)
        }



        /// <summary>
        /// Called every frame, gets secondary movement values from Rewired player
        /// </summary>
        public void SetSiteMovement()
        {
            if ( SmoothMovement )
            {
                _siteMovement.x = _rewiredPlayer.GetAxis(_rewiredActionId_siteHorizontal);
                _siteMovement.y = _rewiredPlayer.GetAxis(_rewiredActionId_siteVertical);
            }
            else
            {
                _siteMovement.x = _rewiredPlayer.GetAxisRaw(_rewiredActionId_siteHorizontal);
                _siteMovement.y = _rewiredPlayer.GetAxisRaw(_rewiredActionId_siteVertical);
            }
        }


        /// <summary>
        /// Called every frame, gets secondary movement values from Rewired player
        /// </summary>
        public void SetUIMovement()
        {
            if ( SmoothMovement )
            {
                _UIMovement.x = _rewiredPlayer.GetAxis(_rewiredActionId_UIHorizontal);
                _UIMovement.y = _rewiredPlayer.GetAxis(_rewiredActionId_UIVertical);
            }
            else
            {
                _UIMovement.x = _rewiredPlayer.GetAxisRaw(_rewiredActionId_UIHorizontal);
                _UIMovement.y = _rewiredPlayer.GetAxisRaw(_rewiredActionId_UIVertical);
            }
        }



        /// <summary>
        /// This is not used.
        /// </summary>
        /// <param name="movement">Movement.</param>
        public override void SetMovement(Vector2 movement)
        {
            if ( IsMobile && InputDetectionActive )
            {
                _primaryMovement.x = movement.x;
                _primaryMovement.y = movement.y;
            }
        }

        /// <summary>
        /// PlayerIDとアクション名を組み合わせた文字列から、アクション名を取得する。
        /// </summary>
        /// <param name="action">PlayerIDを前置詞としたアクション文字列</param>。
        /// <returns>PlayerIDを除いたアクションの文字列。
        private string StripPlayerIdFromActionName(string action)
        {
            if ( string.IsNullOrEmpty(action) )
                return string.Empty;
            if ( !action.StartsWith(PlayerID) )
                return action;
            return action.Substring(PlayerID.Length + 1); // strip PlayerID and underscore
        }

        /// <summary>
        /// Converts button input into MMInput.ButtonStates.
        /// </summary>
        /// <param name="player">The Rewired Player.</param>
        /// <param name="actionId">The Action Id.</param>
        /// <returns>Button state</returns>
        private static MMInput.ButtonStates GetButtonState(Rewired.Player player, int actionId)
        {
            MMInput.ButtonStates state = MMInput.ButtonStates.Off;
            if ( player.GetButton(actionId) )
                state = MMInput.ButtonStates.ButtonPressed;
            if ( player.GetButtonDown(actionId) )
                state = MMInput.ButtonStates.ButtonDown;
            if ( player.GetButtonUp(actionId) )
                state = MMInput.ButtonStates.ButtonUp;
            return state;
        }

        /// <summary>
        /// Gets the Rewired Action Id for an Action name string.
        /// </summary>
        /// <param name="actionName">The Action name.</param>
        /// <param name="warn">Log a warning if the Action does not exist?</param>
        /// <returns>Returns the Action id or -1 if the Action does not exist.</returns>
        public static int GetRewiredActionId(string actionName, bool warn)
        {
            if ( string.IsNullOrEmpty(actionName) )
                return -1;
            int id = ReInput.mapping.GetActionId(actionName);
            if ( id < 0 && warn )
                Debug.LogWarning("No Rewired Action found for Action name \"" + actionName + "\"");
            return id;
        }

        /// <summary>
        /// 何かボタンがいじられてるかどうか
        /// </summary>
        /// <returns></returns>
        public bool CheckButtonUsing()
        {
            return (_inputCheck);
        }


    }
}