using MyTool.Collections;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.BaseController;
using static CharacterController.BrainStatus;


namespace CharacterController
{
    /// <summary>
    /// 次のフレームで全体に適用するイベントリスト、みたいなのを持って、そこにキャラクターがデータを渡せるようにする
    /// Dispose必須。AI関連のDispose()はここでやる責任がある。
    /// </summary>
    public class AIManager : MonoBehaviour, IDisposable
    {

        #region 定義

        /// <summary>
        /// AIのイベントのタイプ。<br/>
        /// 各イベントはチームのヘイトをいじるか、イベントフラグを立てるかの動作。<br/>
        /// 行動に応じてフラグを立て、そのキャラの所属によって解釈が変わる。
        /// </summary>
        [Flags]
        public enum BrainEventFlagType
        {
            None = 0,  // フラグなしの状態を表す基本値
            大ダメージを与えた = 1 << 0,   // 相手に大きなダメージを与えた
            大ダメージを受けた = 1 << 1,   // 相手から大きなダメージを受けた
            回復を使用 = 1 << 2,         // 回復アビリティを使用した
            支援を使用 = 1 << 3,         // 支援アビリティを使用した
                                    //誰かを倒した = 1 << 4,        // 敵または味方を倒した
                                    //指揮官を倒した = 1 << 5,      // 指揮官を倒した
            攻撃対象指定 = 1 << 4,        // 指揮官による攻撃対象の指定
            威圧 = 1 << 5,//威圧状態だと敵が怖がる？ これはバッドステータスでもいいとは思う
        }

        /// <summary>
        /// AIのキャライベントの送信先。
        /// これをシングルトンに置いてイベント提出先にする。
        /// 時間経過したらそのキャラからフラグを消すための設定。立てたフラグを消すためにデータを保持する。
        /// イベント追加時に対象キャラにフラグを設定し、条件を満たしたら立てたフラグを消す。
        /// 時間経過の他、キャラ死亡時もここに問い合わせないと。
        /// 対象キャラのハッシュが死亡キャラハッシュと一致するイベントを線形探索して削除とか。
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct BrainEventContainer
        {

            /// <summary>
            /// イベントのタイプ
            /// </summary>
            public BrainEventFlagType eventType;

            /// <summary>
            /// イベントを呼んだ人のハッシュ。
            /// 
            /// </summary>
            public int targetHash;

            /// <summary>
            /// イベント開始時間
            /// </summary>
            public float startTime;

            /// <summary>
            /// イベントがどれくらいの間保持されるか、という時間。
            /// </summary>
            public float eventHoldTime;

            /// <summary>
            /// AIのイベントのコンストラクタ。
            /// startTimeは現在時を入れる。
            /// </summary>
            /// <param name="brainEvent"></param>
            /// <param name="hashCode"></param>
            /// <param name="holdTime"></param>
            public BrainEventContainer(BrainEventFlagType brainEvent, int hashCode, float holdTime)
            {
                this.eventType = brainEvent;
                this.targetHash = hashCode;
                this.startTime = 0;//GameManager.instance.NowTime;
                this.eventHoldTime = holdTime;
            }

        }

        #endregion 定義

        /// <summary>
        /// シングルトンのインスタンス。
        /// </summary>
        public static AIManager instance;

        /// <summary>
        /// キャラデータを保持するコレクション。<br/>
        /// Jobシステムに渡す時はCharacterDataのUnsafeListにする。<br/>
        /// 座標だけはIJobParallelForTransformで取得する？　せっかくだしLocalScale（＝キャラの向き）まで取っておくといいかも。<>br/>
        /// 向きなんて方向転換した時にデータ書き換えればいいだけかも
        /// </summary>
        public CharacterDataContainer<CharacterData, BaseController> charaDataDictionary = new(7);

        /// <summary>
        /// プレイヤー、敵、その他、それぞれが敵対している陣営をビットで表現。<br/>
        /// キャラデータのチーム設定と一緒に使う<br/>
        /// </summary>
        public static NativeArray<int> relationMap = new(3, Allocator.Persistent);

        /// <summary>
        /// 陣営ごとに設定されたヘイト値。<br/>
        /// ハッシュキーにはゲームオブジェクトのハッシュ値とチームの情報を渡す<br/>
        /// (チーム値,ハッシュ値)という形式<br/>
        /// </summary>
        public NativeHashMap<int2, int> teamHate = new(7, Allocator.Persistent);

        /// <summary>
        /// AIのイベントを受け付ける入れ物。
        /// 時間管理のために使う。
        /// Jobシステムで一括で時間見るか、普通にループするか（イベントはそんなに数がなさそうだし普通が速いかも）
        /// </summary>
        public UnsafeList<BrainEventContainer> eventContainer = new(7, Allocator.Persistent);

        /// <summary>
        /// 行動決定データ。
        /// Jobの書き込み先で、ターゲット変更の反映とかも全部はいってる。<br/>
        /// これを受け取ってキャラクターが行動する。
        /// </summary>
        public UnsafeList<MovementInfo> judgeResult = new(7, Allocator.Persistent);

        /// <summary>
        /// 前回判断を実行した際の時間を記録する。
        /// これを使用して判断結果を受けたオブジェクトたちが前回判定時間を再度設定する。
        /// </summary>
        public float lastJudgeTime;

        /// <summary>
        /// 起動時にシングルトンのインスタンス作成。
        /// </summary>
        private void Awake()
        {
            if ( instance == null )
            {
                instance = this; // this を代入
                DontDestroyOnLoad(this); // シーン遷移時に破棄されないようにする
            }
            else
            {
                Destroy(this);
            }
        }

        /// <summary>
        /// ここで毎フレームジョブを発行する。
        /// </summary>
        private void Update()
        {
            // 毎フレームジョブ実行
            this.BrainJobAct();
        }

        /// <summary>
        /// 新規キャラクターを追加する。
        /// </summary>
        /// <param name="data"></param>
        /// <param name="hashCode"></param>
        /// <param name="team"></param>
        public void CharacterAdd(BrainStatus status, GameObject addObject)
        {
            // 初期所属を追加
            int teamNum = (int)status.baseData.initialBelong;
            int hashCode = addObject.GetHashCode();

            // キャラデータを追加し、敵対する陣営のヘイトリストにも入れる。
            _ = this.charaDataDictionary.AddByHash(hashCode, new CharacterData(status, addObject));

            for ( int i = 0; i < (int)CharacterSide.指定なし; i++ )
            {
                if ( teamNum == i )
                {
                    continue;
                }

                // 敵対チェック
                if ( this.CheckTeamHostility(i, teamNum) )
                {
                    // ひとまずヘイトの初期値は10とする。
                    this.teamHate.Add(new int2(i, hashCode), 10);
                }
            }
        }

        /// <summary>
        /// 退場キャラクターを削除する。
        /// Dispose()してるから、陣営変更の寝返り処理とかで使いまわさないようにね
        /// </summary>
        /// <param name="hashCode"></param>
        /// <param name="team"></param>
        public void CharacterDead(int hashCode, CharacterSide team)
        {

            // 削除の前に値を処理する。
            this.charaDataDictionary[hashCode].Dispose();

            // キャラデータを削除し、敵対する陣営のヘイトリストからも消す。
            _ = this.charaDataDictionary.RemoveByHash(hashCode);

            for ( int i = 0; i < (int)CharacterSide.指定なし; i++ )
            {
                int2 checkTeam = new(i, hashCode);

                // 含むかをチェック
                if ( this.teamHate.ContainsKey(checkTeam) )
                {
                    _ = this.teamHate.Remove(checkTeam);
                }
            }

            // 消えるキャラに紐づいたイベントを削除。
            // 安全にループ内で削除するために後ろから前へとループする。
            for ( int i = this.eventContainer.Length - 1; i > 0; i-- )
            {
                // 消えるキャラにハッシュが一致するなら。
                if ( this.eventContainer[i].targetHash == hashCode )
                {
                    this.eventContainer.RemoveAtSwapBack(i);
                }
            }
        }

        /// <summary>
        /// 二つのチームが敵対しているかをチェックするメソッド。
        /// </summary>
        /// <param name="team1"></param>
        /// <param name="team2"></param>
        /// <returns></returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckTeamHostility(int team1, int team2)
        {
            return (relationMap[team1] & (1 << team2)) > 0;
        }

        /// <summary>
        /// NativeContainerを削除する。
        /// </summary>
        public void Dispose()
        {
            for ( int i = 0; i < 3; i++ )
            {
                this.charaDataDictionary[i].Dispose();
                this.teamHate.Dispose();
            }

            this.eventContainer.Dispose();
            this.teamHate.Dispose();
            relationMap.Dispose();

            Destroy(instance);
        }

        /// <summary>
        /// 毎フレームジョブを実行する。
        /// </summary>
        private void BrainJobAct()
        {
            // キャラの数。
            int _characterCount = this.charaDataDictionary.Count;

            // ジョブの処理対象データの分割数。
            int _jobBatchCount;

            //  キャラクター数に対してバッチカウントの最適化
            if ( _characterCount <= 32 )
            {
                _jobBatchCount = 1;
            }
            else if ( _characterCount <= 128 )
            {
                _jobBatchCount = 16;
            }
            else if ( _characterCount <= 512 )
            {
                _jobBatchCount = 64;
            }
            else // 513〜1000
            {
                _jobBatchCount = 128;
            }

            JobAI brainJob = new()
            {
                // データの引き渡し。
                relationMap = relationMap,
                characterData = this.charaDataDictionary.GetInternalList1ForJob(),
                teamHate = this.teamHate,
                // nowTime = GameManager.instance.NowTime,
                judgeResult = this.judgeResult
            };

            // ジョブ実行。
            JobHandle handle = brainJob.Schedule(brainJob.characterData.Length, _jobBatchCount);

            // ジョブの完了を待機
            handle.Complete();

        }

    }

}

