using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 全ての行動の基底クラス
/// モーション、移動距離、接触モードを持つ
/// エフェクト関連は別に持つ。
/// </summary>
public class ActionData
{
    /// <summary>
    /// モーション移動の状態を表す列挙型。
    /// 
    /// </summary>
    public enum RushState : byte
    {
        停止,
        待機,
        移動
    }

    /// <summary>
    /// モーション移動中、敵と接触した際の行動。
    /// </summary>
    public enum MoveContactType : byte
    {
        通過,//通り抜ける。接触ない
        停止,//敵と接触したら止まる
        押す//敵を押して進んでいく
    }

    /// <summary>
    /// アクションの特徴
    /// アニメイベントで指定した時間で始動
    /// </summary>
    public enum ActionFeature : byte
    {
        無敵,
        スーパーアーマー,// 攻撃アーマーとは異なり完全に怯まない
    }

    /// <summary>
    /// アクション時の移動の緩急についての設定
    /// </summary>
    public enum MoveSpeedType : byte
    {
        等速 = 0,
        等加速,
        指数的に加速,
        指数的に加速後に減速,
        ゆっくり動いた後急加速,
    }


    [Header("アクション移動の時間")]
    /// <summary>
    /// 移動する時間
    /// </summary>
    public float moveDuration;

    [Header("アクション移動の距離")]
    /// <summary>
    /// 攻撃の移動距離
    /// ロックオンする場合はこの範囲内で敵との距離を入れる
    /// 速度は移動距離を時間で割った速さ
    /// </summary>
    public float2 moveDistance;

    [Header("敵キャラと接触時の挙動")]
    /// <summary>
    /// 行動移動中に敵と接触時の挙動
    /// </summary>
    public MoveContactType contactType;

    [Header("移動を開始するまでの時間")]
    /// <summary>
    /// 移動開始するまでの時間
    /// </summary>
    public float startMoveTime;

    /// <summary>
    /// アクション時の移動速度の特徴
    /// </summary>
    [Header("アクション時の移動速度の特徴")]
    public MoveSpeedType speedType;

    [Header("ロックオンするか")]
    /// <summary>
    /// ロックオンして移動するかどうか
    /// ロックオンしたら移動距離が伸びたりして何としてでもターゲットの前に行く
    /// ターゲットは原則いるはずなので
    /// </summary>
    public bool lockAction;

    /// <summary>
    /// アーマー
    /// </summary>
    [Header("行動時の追加アーマー")]
    public int additionalArmor;//g

    [Header("モーション")]
    /// <summary>
    /// アクションで使用するモーション。
    /// </summary>
    public Animation motionAnime;

    [Header("次のアクション")]
    /// <summary>
    /// 次にチェインするアクション
    /// 妨害されなければ自動で実行する。
    /// </summary>
    public ActionData nextAction;
}
