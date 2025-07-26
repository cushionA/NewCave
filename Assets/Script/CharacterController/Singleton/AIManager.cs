using CharacterController.Collections;
using CharacterController.StatusData;
using Rewired;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.BaseController;
using static CharacterController.StatusData.BrainStatus;
using static SensorSystem;

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
        public enum BrainEventFlagType : byte
        {
            None = 0,  // フラグなしの状態を表す基本値
            大ダメージを与えた = 1 << 0,   // 相手に大きなダメージを与えた
            大ダメージを受けた = 1 << 1,   // 相手から大きなダメージを受けた
            回復を使用 = 1 << 2,         // 回復アビリティを使用した
            支援を使用 = 1 << 3,         // 支援アビリティを使用した
                                    //誰かを倒した = 1 << 4,        // 敵または味方を倒した
                                    //指揮官を倒した = 1 << 5,      // 指揮官を倒した
                                    // コメントアウト部分は指示による指定を撃破時にすればいい
            攻撃対象指定 = 1 << 4,        // 指揮官による攻撃対象の指定
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

        /// <summary>
        /// 各キャラクターのセンサーが tag 認識時に認識データに加える操作を定義するデリゲート。
        /// </summary>
        /// <param name="data">操作対象の認識データ</param>
        public delegate RecognitionLog RecognizeTagDelegate(ref RecognitionData data, GameObject obj);

        #region タグ設定

        /// <summary>
        /// ゲーム内で使用するタグの文字列定数を管理するクラス
        /// 全てのタグ文字列はここで一元管理される
        /// </summary>
        public static class TagConstants
        {
            // ================================
            // オブジェクトタグ定数
            // ================================

            /// <summary>キャラクターを示すタグ</summary>
            public const string CHARACTER = "Character";

            /// <summary>アイテムオブジェクトを示すタグ(装備品、消費アイテムなど)</summary>
            public const string ITEM = "Item";

            /// <summary>危険物を示すタグ(火薬の箱など)</summary>
            public const string HAZARD = "Hazard";

            /// <summary>攻撃の投射物を示すタグ</summary>
            public const string SHOOT = "Shoot";

            /// <summary>支援エリア示すタグ</summary>
            public const string BUFF_AREA = "BuffArea";

            /// <summary>弱体エリア示すタグ</summary>
            public const string DEBUFF_AREA = "DebuffArea";

            /// <summary>破壊可能オブジェクトを示すタグ(箱、壁、バリケードなど)</summary>
            public const string DESTRUCTIBLE = "Destructible";

            // ================================
            // 地形・環境タグ定数
            // ================================

            /// <summary>水域を示すタグ(川、湖、海など)</summary>
            public const string WATER = "Water";

            /// <summary>崖やすり抜けて登れるポイントを示すタグ(よじ登れる地形)</summary>
            public const string ClimbPoint = "ClimbPoint";

        }

        #endregion

        #endregion 定義

        #region 定数

        /// <summary>
        /// 認識データの更新用デリゲートの辞書。
        /// </summary>
        public readonly Dictionary<string, RecognizeTagDelegate> recognizeTagAction = new Dictionary<string, RecognizeTagDelegate>
        {
            // オブジェクトタグ
    { TagConstants.CHARACTER, (ref RecognitionData data, GameObject obj) => {
        
        // 所属を取得してキャラの情報を反映する。
        CharacterBelong belong = AIManager.instance.characterDataDictionary.GetBelong(obj);

        // 所属によって処理が分岐
        switch (belong)
    {
        case CharacterBelong.プレイヤー:
         data.recognizeObject |= RecognizeObjectType.プレイヤー側キャラ;
         return new RecognitionLog(obj, RecognizeObjectType.プレイヤー側キャラ);
        case CharacterBelong.魔物:
         data.recognizeObject |= RecognizeObjectType.魔物側キャラ;
         return new RecognitionLog(obj, RecognizeObjectType.魔物側キャラ);
        default:
        data.recognizeObject |= RecognizeObjectType.中立側キャラ;
        return new RecognitionLog(obj, RecognizeObjectType.中立側キャラ);
    }
    } },
    { TagConstants.ITEM, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.アイテム;
        return new RecognitionLog(obj, RecognizeObjectType.アイテム);
    } },
    { TagConstants.HAZARD, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.危険物;
        return new RecognitionLog(obj, RecognizeObjectType.危険物);
    } },
    { TagConstants.BUFF_AREA, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.バフエリア;
        return new RecognitionLog(obj, RecognizeObjectType.バフエリア);
    } },
    { TagConstants.DEBUFF_AREA, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.デバフエリア;
        return new RecognitionLog(obj, RecognizeObjectType.デバフエリア);
    } },
    { TagConstants.DESTRUCTIBLE, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.破壊可能オブジェクト;
        return new RecognitionLog(obj, RecognizeObjectType.破壊可能オブジェクト);
    } },
    
    // 地形・環境タグ
    { TagConstants.WATER, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.水場;
        return new RecognitionLog(obj, RecognizeObjectType.水場);
    } },
    { TagConstants.ClimbPoint, (ref RecognitionData data, GameObject obj) => {
        data.recognizeObject |= RecognizeObjectType.よじ登りポイント;
        return new RecognitionLog(obj, RecognizeObjectType.よじ登りポイント);
    } }
};

        #endregion

        #region フィールド

        /// <summary>
        /// キャラデータの管理用データ構造
        /// </summary>
        public CharacterStatusList brainStatusList;

        /// <summary>
        /// シングルトンのインスタンス。
        /// </summary>
        [HideInInspector]
        public static AIManager instance;

        /// <summary>
        /// キャラデータを保持するコレクション。<br/>
        /// Jobシステムに渡す時はCharacterDataのUnsafeListにする。<br/>
        /// 座標だけはIJobParallelForTransformで取得する？　せっかくだしLocalScale（＝キャラの向き）まで取っておくといいかも。<>br/>
        /// 向きなんて方向転換した時にデータ書き換えればいいだけかも
        /// </summary>
        [HideInInspector]
        public SoACharaDataDic characterDataDictionary;

        /// <summary>
        /// プレイヤー、敵、その他、それぞれが敵対している陣営をビットで表現。<br/>
        /// キャラデータのチーム設定と一緒に使う<br/>
        /// </summary>
        [HideInInspector]
        public NativeArray<int> relationMap;

        /// <summary>
        /// 陣営ごとに設定されたヘイト値。<br/>
        /// ハッシュキーにはゲームオブジェクトのハッシュ値とチームの情報を渡す<br/>
        /// (チーム値,ハッシュ値)という形式<br/>
        /// </summary>
        [HideInInspector]
        public NativeHashMap<int2, int> teamHate;

        /// <summary>
        /// 個人用のヘイト値。<br/>
        /// ハッシュキーにはゲームオブジェクトのハッシュ値と相手のハッシュを渡す<br/>
        /// (自分のハッシュ,相手のハッシュ値)という形式<br/>
        /// </summary>
        [HideInInspector]
        public NativeHashMap<int2, int> personalHate;

        /// <summary>
        /// AIのイベントを受け付ける入れ物。
        /// 時間管理のために使う。
        /// Jobシステムで一括で時間見るか、普通にループするか（イベントはそんなに数がなさそうだし普通が速いかも）
        /// </summary>
        [HideInInspector]
        public UnsafeList<BrainEventContainer> eventContainer;

        /// <summary>
        /// 行動決定データ。
        /// Jobの書き込み先で、ターゲット変更の反映とかも全部はいってる。<br/>
        /// これを受け取ってキャラクターが行動する。
        /// </summary>
        [HideInInspector]
        public UnsafeList<MovementInfo> judgeResult;

        #endregion フィールド

        #region 初期化

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

        private void Start()
        {
            // キャラデータの初期化
            this.characterDataDictionary = new SoACharaDataDic(130, Allocator.Persistent);

            // AIのイベントコンテナを初期化
            this.eventContainer = new UnsafeList<BrainEventContainer>(7, Allocator.Persistent);

            // ジャッジ結果の初期化
            this.judgeResult = new UnsafeList<MovementInfo>(130, Allocator.Persistent);

            // キャラステータスリストの初期化
            this.brainStatusList = Resources.Load<CharacterStatusList>("CharacterStatusList");

            // タグハンドルの初期化
            this.InitializeTagHandles();

            // 関係マップの初期化
            this.relationMap = new NativeArray<int>(3, Allocator.Persistent);
        }

        #endregion 初期化

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
        public void CharacterAdd(BrainStatus status, BaseController addCharacter)
        {
            // 初期所属を追加
            int teamNum = (int)status.baseData.initialBelong;
            int hashCode = addCharacter.gameObject.GetHashCode();

            // キャラデータを追加し、敵対する陣営のヘイトリストにも入れる。
            _ = this.characterDataDictionary.Add(status, addCharacter, hashCode);

            for ( int i = 0; i < (int)CharacterBelong.指定なし; i++ )
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
        public void CharacterDead(int hashCode, CharacterBelong team)
        {

            // キャラデータを削除し、敵対する陣営のヘイトリストからも消す。
            _ = this.characterDataDictionary.RemoveByHash(hashCode);

            for ( int i = 0; i < (int)CharacterBelong.指定なし; i++ )
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
            return (this.relationMap[team1] & (1 << team2)) > 0;
        }

        /// <summary>
        /// NativeContainerを削除する。
        /// </summary>
        public void Dispose()
        {
            this.characterDataDictionary.Dispose();
            this.teamHate.Dispose();
            this.eventContainer.Dispose();
            this.teamHate.Dispose();
            this.relationMap.Dispose();

            Destroy(instance);
        }

        #region 内部メソッド

        /// <summary>
        /// タグハンドルの初期化
        /// ゲーム開始時に一度だけ実行される
        /// </summary>
        private void InitializeTagHandles()
        {
            objectTags = new ObjectTags();
            terrainTags = new TerrainTags();
            Debug.Log("AIManager: TagHandle初期化完了");
        }

        #endregion


        /// <summary>
        /// 毎フレームジョブを実行する。
        /// </summary>
        private void BrainJobAct()
        {

            (UnsafeList<BrainStatus.CharacterBaseInfo> characterBaseInfo,
                             UnsafeList<BrainStatus.SolidData> solidData,
             UnsafeList<BrainStatus.CharacterAtkStatus> characterAtkStatus,
             UnsafeList<BrainStatus.CharacterDefStatus> characterDefStatus,
             UnsafeList<BrainStatus.CharacterStateInfo> characterStateInfo,
             UnsafeList<BrainStatus.MoveStatus> moveStatus,
             UnsafeList<BrainStatus.CharacterColdLog> coldLog, UnsafeList<RecognitionData> recognitions) = this.characterDataDictionary;

            JobAI brainJob = new((
                characterBaseInfo, characterAtkStatus, characterDefStatus, solidData, characterStateInfo, moveStatus, coldLog, recognitions),
                this.personalHate,
                this.teamHate,
                this.judgeResult,
                this.relationMap,
                this.brainStatusList.brainArray,
                0 // いずれゲームマネージャーの変数と置き換える
            );

            // ジョブ実行。
            JobHandle handle = brainJob.Schedule(this.characterDataDictionary.Count, 1);

            // ジョブの完了を待機
            handle.Complete();

        }

    }

}

