using CharacterController.StatusData;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using static CharacterController.StatusData.BrainStatus;

namespace CharacterController
{
    /// <summary>
    /// キャラクターコントローラーに当たるクラス。<br/>
    /// このクラスが持つのはキャラクターの不変、かつ参照型を中心にしたステータスデータだけ。<br/>
    /// AIのJobが出力した状況判断結果データを受け取って、その通りに動く。<br/>
    /// それらのデータはScriptableObjectから取得するため、実質的にはこのクラス自体が持つデータは判断結果だけ。<br/>
    /// 判断の過程は隠蔽し、切り離した上で行動に必要なデータだけを持つ。
    /// </summary>
    public class BaseController : MonoBehaviour
    {
        #region 定義

        #region enum定義

        /// <summary>
        /// 判断結果をまとめて格納するビット演算用
        /// </summary>
        [Flags]
        public enum JudgeResult
        {
            何もなし = 0,
            新しく判断をした = 1 << 1,// この時は移動方向も変える
            方向転換をした = 1 << 2,
            状態を変更した = 1 << 3,
        }

        /// <summary>
        /// キャラの特殊行動を記録するためのフラグ列挙型。
        /// いらなそう。対応する行動をしたやつのチームヘイトを上げたりすればいいだけ。
        /// </summary>
        public enum CharacterActionLog
        {
            回復した,
            回復された,
            大ダメージを受けた,
            魔物に大ダメージを与えた,

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
            public int targetDirection;

            /// <summary>
            /// 番号で行動を指定する。
            /// 攻撃に限らず逃走とかも全部。移動方向から使用モーション、行動の種別まで（魔法とか移動とか）
            /// こっちの構造データはステータスに持たせとこ。行動状態ごとに番号で指定された行動をする。
            /// 状態変更の場合、これで変更先の状態を指定する。
            /// </summary>
            public int actNum;

            /// <summary>
            /// 判断結果についての情報を格納するビット
            /// </summary>
            public JudgeResult result;

            /// <summary>
            /// 現在の行動状態。
            /// </summary>
            public ActState moveState;

            /// <summary>
            /// デバッグ用。
            /// 選択した行動条件を設定する。
            /// </summary>
            public int selectActCondition;

            /// <summary>
            /// デバッグ用。
            /// 選択したターゲット選択条件を設定する。
            /// </summary>
            public int selectTargetCondition;

            /// <summary>
            /// 新規判断時の処理
            /// 行動終了後？
            /// </summary>
            public void JudgeUpdate(int hashCode)
            {
                // 判断情報をキャラデータに反映する。
                // 時間に関してはゲームマネージャー実装後にマネージャーからとるように変更するよ。
                AIManager.instance.characterDataDictionary.UpdateDataAfterJudge(hashCode, moveState, result == JudgeResult.新しく判断をした ? actNum : -1, 0);
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

        #endregion

        /// テストで使用するステータス。<br></br>
        /// 判断間隔のデータが入っている。<br></br>
        /// インスペクタから設定。
        /// </summary>
        [SerializeField]
        protected BrainStatus status;

        /// <summary>
        /// 移動に使用する物理コンポーネント。
        /// </summary>
        [SerializeField]
        private Rigidbody2D _rb;

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
        public int objectHash;

        /// <summary>
        /// 初期化処理。
        /// </summary>
        protected void Initialize()
        {
            // 新しいキャラデータを送り、コンバットマネージャーに送る。
            // いや、やっぱり材料送って向こうで作ってもらおう。
            // NativeContainer含む構造体をコピーするのなんかこわい。
            // ただもしコピーしても、こっちで作った分はローカル変数でしかないからDispose()周りの問題はないはず。
            AIManager.instance.CharacterAdd(this.status, this);
        }

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

    }
}

