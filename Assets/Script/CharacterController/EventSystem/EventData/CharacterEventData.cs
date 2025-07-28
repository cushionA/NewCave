namespace CharacterController.EventSystem
{
    /// <summary>
    /// 外部からのアクションに対して反応するためのイベントデータ。
    /// こちらでは状態を変更するためのイベントデータを定義します。
    /// </summary>
    public struct ReactionEventData
    {
        public enum ReactionType
        {
            なし = 0,
            被弾 = 1 << 0,
            大ダメージ = 1 << 1,
            連続被弾 = 1 << 2,// 範囲攻撃に巻き込まれたとかの検出にも使えそう
            近くに敵がいる = 1 << 3,// センサー機能で敵の接近を検出
        }

        public ReactionType Type;

    }

    /// <summary>
    /// 外部からの命令や行動指定を行うためのイベントデータ。
    /// こちらではデータを保持することはなく、その場で受け入れるかどうかを決める。
    /// 受け入れた後は指定行動のデータと指定レベルをもって、より上のレベルの指定が来るか実行によってそのデータも消す。
    /// 実行には指定実行回数があり、それを消化するまではデータが残る。
    /// あるいは即実行できるものだけにする？
    /// </summary>
    public struct CommandEventData
    {
        public enum CommandType
        {
            None,
            Move,
            Attack,
            Defend,
            UseItem,
            Interact
        }

        public CommandType Type;

    }

}

