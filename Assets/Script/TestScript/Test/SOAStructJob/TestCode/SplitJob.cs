using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static global::TestScript.SOATest.SOAStatus;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace SplitJob
{
    /// <summary>
    /// AIが判断を行うJob
    /// 判断可能かどうかを見る。
    /// 最終的には判断間隔に加えクールタイム等も見る。
    /// </summary>
    [BurstCompile(
        FloatPrecision = FloatPrecision.Medium,
        FloatMode = FloatMode.Fast,
        DisableSafetyChecks = true,
        OptimizeFor = OptimizeFor.Performance
    )]
    public struct FirstJob : IJobParallelFor
    {
        /// <summary>
        /// 参照頻度の低いデータ
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharaColdLog> _coldLog;

        /// <summary>
        /// 現在時間
        /// </summary>
        [ReadOnly]
        public float nowTime;

        [WriteOnly]
        public UnsafeList<int> stateList;

        /// <summary>
        /// キャラのAIの設定。
        /// 状態に基づいて最初にデータを一つだけ抜く。
        /// </summary>
        [ReadOnly]
        public NativeArray<BrainDataForJob> brainArray;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="dataLists"></param>
        /// <param name="teamHate"></param>
        /// <param name="judgeResult"></param>
        /// <param name="relationMap"></param>
        /// <param name="brainArray"></param>
        /// <param name="nowTime"></param>
        public FirstJob(UnsafeList<int> stateList, UnsafeList<CharaColdLog> coldLog,
             NativeArray<BrainDataForJob> brainArray, float nowTime)
        {
            // タプルから各データリストを展開してフィールドに代入
            this._coldLog = coldLog;
            this.stateList = stateList;

            this.brainArray = brainArray;
            this.nowTime = nowTime;
        }

        /// <summary>
        /// characterDataとjudgeResultのインデックスをベースに処理する。
        /// </summary>
        /// <param name="index"></param>
        public void Execute(int index)
        {
            // インターバルをまとめて取得
            // xが行動、yが移動判断
            float2 intervals = this.brainArray[this._coldLog[index].characterID - 1].GetInterval();

            // 判断時間が経過したかを確認。
            // 経過してないなら処理しない。
            // あるいはターゲット消えた場合も判定したい。チームヘイトに含まれてなければ。それだと味方がヘイトの時どうするの。
            // キャラ死亡時に全キャラに対しターゲットしてるかどうかを確認するようにしよう。で、ターゲットだったら前回判断時間をマイナスにする。
            this.stateList[index] = this.nowTime - this._coldLog[index].lastJudgeTime < intervals.x
                ? math.select(-1, -2, this.nowTime - this._coldLog[index].lastJudgeTime < intervals.y)
                : 0;

        }
    }

    /// <summary>
    /// AIが判断を行うJob
    /// 流れとしてはヘイト判断（ここで一番憎いヤツは出しておく）→行動判断→対象設定（攻撃/防御の場合ヘイト、それ以外の場合は任意条件を優先順に判断）
    /// ヘイト処理はチームヘイトが一番高いやつを陣営ごとに出しておいて、個人ヘイト足したらそれを超えるか、で見ていこうか
    /// UnsafeList<CharacterData> characterDataは論理削除で中身ないデータもあるからその判別もしないとな
    /// </summary>
    [BurstCompile(
        FloatPrecision = FloatPrecision.Medium,
        FloatMode = FloatMode.Fast,
        DisableSafetyChecks = true,
        OptimizeFor = OptimizeFor.Performance
    )]
    public struct SecondJob : IJobParallelFor
    {
        /// <summary>
        /// 読み取り専用のチームごとの全体ヘイト
        /// </summary>
        [ReadOnly]
        public NativeHashMap<int2, int> teamHate;

        /// <summary>
        /// キャラクターの基本情報
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterBaseInfo> _characterBaseInfo;

        /// <summary>
        /// AIが参照するための状態情報
        /// </summary>
        [ReadOnly]
        public UnsafeList<SolidData> _solidData;

        /// <summary>
        /// AIが参照するための状態情報
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterStateInfo> _characterStateInfo;

        /// <summary>
        /// 参照頻度の低いデータ
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharaColdLog> _coldLog;

        /// <summary>
        /// キャラごとの個人ヘイト管理用
        /// 自分のハッシュと相手のハッシュをキーに値を持つ。
        /// </summary>
        [ReadOnly]
        public NativeHashMap<int2, int> pHate;

        /// <summary>
        /// キャラのAIの設定。
        /// 状態に基づいて最初にデータを一つだけ抜く。
        /// </summary>
        [ReadOnly]
        public NativeArray<BrainDataForJob> brainArray;

        [WriteOnly]
        public UnsafeList<int> _selectMoveList;

        [ReadOnly]
        public UnsafeList<int> _stateList;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="dataLists"></param>
        /// <param name="teamHate"></param>
        /// <param name="judgeResult"></param>
        /// <param name="relationMap"></param>
        /// <param name="brainArray"></param>
        /// <param name="nowTime"></param>
        public SecondJob((
        UnsafeList<CharacterBaseInfo> characterBaseInfo,
        UnsafeList<CharacterAtkStatus> characterAtkStatus,
        UnsafeList<CharacterDefStatus> characterDefStatus,
        UnsafeList<SolidData> solidData,
        UnsafeList<CharacterStateInfo> characterStateInfo,
        UnsafeList<MoveStatus> moveStatus,
        UnsafeList<CharaColdLog> coldLog
        ) dataLists, NativeHashMap<int2, int> pHate, NativeHashMap<int2, int> teamHate, UnsafeList<CharacterController.BaseController.MovementInfo> judgeResult,
            NativeArray<int> relationMap, NativeArray<BrainDataForJob> brainArray, UnsafeList<int> selectMoveList, UnsafeList<int> stateList)
        {
            // タプルから各データリストを展開してフィールドに代入
            this._characterBaseInfo = dataLists.characterBaseInfo;
            this._solidData = dataLists.solidData;
            this._characterStateInfo = dataLists.characterStateInfo;
            this._coldLog = dataLists.coldLog;
            this._selectMoveList = selectMoveList;
            this._stateList = stateList;

            // 個別パラメータをフィールドに代入
            this.pHate = pHate;
            this.teamHate = teamHate;
            this.brainArray = brainArray;
        }

        /// <summary>
        /// characterDataとjudgeResultのインデックスをベースに処理する。
        /// </summary>
        /// <param name="index"></param>
        public void Execute(int index)
        {

            if ( this._stateList[index] < 0 )
            {
                this._selectMoveList[index] = math.select(-1, -2, this._stateList[index] == -1);
                return;
            }

            // 現在の行動のステートを数値に変換
            int nowMode = (int)this._characterStateInfo[index].actState;

            BrainSettingForJob brainData = this.brainArray[this._coldLog[index].characterID - 1].brainSetting[nowMode];

            // 行動条件の中で前提を満たしたものを取得するビット
            // なお、実際の判断時により優先的な条件が満たされた場合は上位ビットはまとめて消す。
            int enableCondition = 0;

            // 前提となる自分についてのスキップ条件を確認。
            // 最後の条件は補欠条件なので無視
            for ( int i = 0; i < brainData.behaviorSetting.Length - 1; i++ )
            {

                SkipJudgeData skipData = brainData.behaviorSetting[i].skipData;

                // スキップ条件を解釈して判断
                if ( skipData.skipCondition == SkipJudgeCondition.条件なし || this.JudgeSkipByCondition(skipData, index) == 1 )
                {
                    enableCondition |= 1 << i;
                }
            }

            // 条件を満たした行動の中で最も優先的なもの。
            // 初期値は最後の条件、つまり条件なしの補欠条件
            int selectMove = brainData.behaviorSetting.Length - 1;

            // キャラデータを確認する。
            for ( int i = 0; i < this._solidData.Length; i++ )
            {
                // 自分はスキップ
                if ( index == i )
                {
                    continue;
                }

                // 行動判断。
                // ここはスイッチ文使おう。連続するInt値ならコンパイラがジャンプテーブル作ってくれるので
                if ( enableCondition != 0 )
                {
                    for ( int j = 0; j < brainData.behaviorSetting.Length - 1; j++ )
                    {
                        // ある条件満たしたらbreakして、以降はそれ以下の条件もう見ない。
                        if ( this.CheckActCondition(brainData.behaviorSetting[j].actCondition, index, i) )
                        {
                            selectMove = j;

                            // enableConditionのbitも消す。
                            // i桁目までのビットをすべて1にするマスクを作成
                            // (1 << (i + 1)) - 1 は 0から i-1桁目までのビットがすべて1
                            int mask = (1 << j) - 1;

                            // マスクと元の値の論理積を取ることで上位ビットをクリア
                            enableCondition = enableCondition & mask;
                            break;
                        }
                    }
                }
                // 条件満たしたらループ終わり。
                else
                {
                    break;
                }

            }

            this._selectMoveList[index] = selectMove;

        }

        #region スキップ条件判断

        /// <summary>
        /// SkipJudgeConditionに基づいて判定を行うメソッド
        /// </summary>
        /// <param name="skipData">スキップ判定用データ</param>
        /// <param name="charaData">キャラクターデータ</param>
        /// <returns>条件に合致する場合は1、それ以外は0</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public int JudgeSkipByCondition(in SkipJudgeData skipData, int myIndex)
        {
            SkipJudgeCondition condition = skipData.skipCondition;
            switch ( condition )
            {
                case SkipJudgeCondition.自分のHPが一定割合の時:
                    // 各条件を個別に int で評価
                    int equalConditionHP = skipData.judgeValue == this._characterBaseInfo[myIndex].hpRatio ? 1 : 0;
                    int lessConditionHP = skipData.judgeValue < this._characterBaseInfo[myIndex].hpRatio ? 1 : 0;
                    int invertConditionHP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                    // 明示的に条件を組み合わせる
                    int condition1HP = equalConditionHP;
                    int condition2HP = lessConditionHP != 0 == (invertConditionHP != 0) ? 1 : 0;
                    if ( condition1HP != 0 || condition2HP != 0 )
                    {
                        return 1;
                    }

                    return 0;

                case SkipJudgeCondition.自分のMPが一定割合の時:
                    // 各条件を個別に int で評価
                    int equalConditionMP = skipData.judgeValue == this._characterBaseInfo[myIndex].mpRatio ? 1 : 0;
                    int lessConditionMP = skipData.judgeValue < this._characterBaseInfo[myIndex].mpRatio ? 1 : 0;
                    int invertConditionMP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                    // 明示的に条件を組み合わせる
                    int condition1MP = equalConditionMP;
                    int condition2MP = lessConditionMP != 0 == (invertConditionMP != 0) ? 1 : 0;
                    if ( condition1MP != 0 || condition2MP != 0 )
                    {
                        return 1;
                    }

                    return 0;

                default:
                    // デフォルトケース（未定義の条件の場合）
                    Debug.LogWarning($"未定義のスキップ条件: {condition}");
                    return 0;
            }
        }

        #endregion スキップ条件判断

        /// <summary>
        /// 行動判断の処理を隔離したメソッド
        /// </summary>
        /// <param name="conditions"></param>
        /// <param name="charaData"></param>
        /// <param name="nowHate"></param>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public bool CheckActCondition(in ActJudgeData condition, int myIndex,
            int targetIndex)
        {
            bool result = true;

            // フィルター通過しないなら戻る。
            if ( condition.filter.IsPassFilter(this._solidData[targetIndex], this._characterStateInfo[targetIndex]) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                case ActJudgeCondition.指定のヘイト値の敵がいる時:

                    int targetHash = this._coldLog[targetIndex].hashCode;
                    int targetHate = 0;
                    int2 pHateKey = new(this._coldLog[myIndex].hashCode, targetHash);

                    if ( this.pHate.TryGetValue(pHateKey, out int hate) )
                    {
                        targetHate += hate;
                    }

                    // チームのヘイトはint2で確認する。
                    int2 hateKey = new((int)this._characterStateInfo[targetIndex].belong, targetHash);

                    if ( this.teamHate.TryGetValue(hateKey, out int tHate) )
                    {
                        targetHate += tHate;
                    }

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE ? targetHate >= condition.judgeValue : targetHate <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.HPが一定割合の対象がいる時:

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].hpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].hpRatio <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.MPが一定割合の対象がいる時:

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].mpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].mpRatio <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.設定距離に対象がいる時:

                    // 二乗の距離で判定する。
                    int judgeDist = condition.judgeValue * condition.judgeValue;

                    // 今の距離の二乗。
                    int distance = (int)math.distancesq(this._characterBaseInfo[targetIndex].nowPosition, this._characterBaseInfo[targetIndex].nowPosition);

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE ? distance >= judgeDist : distance <= judgeDist;

                    return result;

                case ActJudgeCondition.特定の属性で攻撃する対象がいる時:

                    // 通常はいる時、逆の場合はいないとき
                    result = condition.isInvert == BitableBool.FALSE
                        ? ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) > 0
                        : ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) == 0;

                    return result;

                case ActJudgeCondition.特定の数の敵に狙われている時:
                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterStateInfo[targetIndex].targetingCount >= condition.judgeValue
                        : this._characterStateInfo[targetIndex].targetingCount <= condition.judgeValue;

                    return result;

                default: // 条件なし (0) または未定義の値
                    return result;
            }
        }

    }

    [BurstCompile(
    FloatPrecision = FloatPrecision.Medium,
    FloatMode = FloatMode.Fast,
    DisableSafetyChecks = true,
    OptimizeFor = OptimizeFor.Performance
)]
    public struct ThirdJob : IJobParallelFor
    {
        /// <summary>
        /// 読み取り専用のチームごとの全体ヘイト
        /// </summary>
        [ReadOnly]
        public NativeHashMap<int2, int> teamHate;

        /// <summary>
        /// キャラクターの基本情報
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterBaseInfo> _characterBaseInfo;

        /// <summary>
        /// 攻撃力のデータ
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterAtkStatus> _characterAtkStatus;

        /// <summary>
        /// 防御力のデータ
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterDefStatus> _characterDefStatus;

        /// <summary>
        /// AIが参照するための状態情報
        /// </summary>
        [ReadOnly]
        public UnsafeList<SolidData> _solidData;

        /// <summary>
        /// AIが参照するための状態情報
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharacterStateInfo> _characterStateInfo;

        /// <summary>
        /// 移動関連のステータス
        /// </summary>
        [ReadOnly]
        public UnsafeList<MoveStatus> _moveStatus;

        /// <summary>
        /// 参照頻度の低いデータ
        /// </summary>
        [ReadOnly]
        public UnsafeList<CharaColdLog> _coldLog;

        /// <summary>
        /// キャラごとの個人ヘイト管理用
        /// 自分のハッシュと相手のハッシュをキーに値を持つ。
        /// </summary>
        [ReadOnly]
        public NativeHashMap<int2, int> pHate;

        /// <summary>
        /// 行動決定データ。
        /// ターゲット変更の反映とかも全部こっちでやる。
        /// </summary>
        [WriteOnly]
        public UnsafeList<CharacterController.BaseController.MovementInfo> judgeResult;

        /// <summary>
        /// プレイヤー、敵、その他、それぞれが敵対している陣営をビットで表現。
        /// キャラデータのチーム設定と一緒に使う
        /// </summary>
        [ReadOnly]
        public NativeArray<int> relationMap;

        /// <summary>
        /// キャラのAIの設定。
        /// 状態に基づいて最初にデータを一つだけ抜く。
        /// </summary>
        [ReadOnly]
        public NativeArray<BrainDataForJob> brainArray;

        [ReadOnly]
        public UnsafeList<int> _selectMoveList;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="dataLists"></param>
        /// <param name="teamHate"></param>
        /// <param name="judgeResult"></param>
        /// <param name="relationMap"></param>
        /// <param name="brainArray"></param>
        /// <param name="nowTime"></param>
        public ThirdJob((
        UnsafeList<CharacterBaseInfo> characterBaseInfo,
        UnsafeList<CharacterAtkStatus> characterAtkStatus,
        UnsafeList<CharacterDefStatus> characterDefStatus,
        UnsafeList<SolidData> solidData,
        UnsafeList<CharacterStateInfo> characterStateInfo,
        UnsafeList<MoveStatus> moveStatus,
        UnsafeList<CharaColdLog> coldLog
        ) dataLists, NativeHashMap<int2, int> pHate, NativeHashMap<int2, int> teamHate, UnsafeList<CharacterController.BaseController.MovementInfo> judgeResult,
            NativeArray<int> relationMap, NativeArray<BrainDataForJob> brainArray, UnsafeList<int> selectMoveList)
        {
            // タプルから各データリストを展開してフィールドに代入
            this._characterBaseInfo = dataLists.characterBaseInfo;
            this._characterAtkStatus = dataLists.characterAtkStatus;
            this._characterDefStatus = dataLists.characterDefStatus;
            this._solidData = dataLists.solidData;
            this._characterStateInfo = dataLists.characterStateInfo;
            this._moveStatus = dataLists.moveStatus;
            this._coldLog = dataLists.coldLog;
            this._selectMoveList = selectMoveList;

            // 個別パラメータをフィールドに代入
            this.pHate = pHate;
            this.teamHate = teamHate;
            this.judgeResult = judgeResult;
            this.relationMap = relationMap;
            this.brainArray = brainArray;
        }

        /// <summary>
        /// characterDataとjudgeResultのインデックスをベースに処理する。
        /// </summary>
        /// <param name="index"></param>
        public void Execute(int index)
        {
            // 2025/7/28 廃棄

            /*
            // 結果の構造体を作成。
            CharacterController.BaseController.MovementInfo resultData = new();
            int selectMove = this._selectMoveList[index];

            if ( selectMove < 0 )
            {
                resultData.result = selectMove == -1
                    ? CharacterController.BaseController.JudgeResult.方向転換をした
                    : CharacterController.BaseController.JudgeResult.何もなし;

                this.judgeResult[index] = resultData;
                return;
            }

            // 現在の行動のステートを数値に変換
            int nowMode = (int)this._characterStateInfo[index].actState;

            BrainSettingForJob brainData = this.brainArray[this._coldLog[index].characterID - 1].brainSetting[nowMode];

            // その後、二回目のループで条件に当てはまるキャラを探す。
            // 二回目で済むかな？　判断条件の数だけ探さないとダメじゃない？
            // 準備用のジョブで一番攻撃力が高い/低い、とかのキャラを陣営ごとに探しとくべきじゃない？
            // それは明確にやるべき。
            // いや、でも対象を所属や特徴でフィルタリングするならやっぱりダメかも
            // 大人しく条件ごとに線形するか。
            // 救いとなるのは、

            // 距離に関しては別処理を実装すると決めた。
            // kd木や空間分割データ構造とかあるみたいだけど、更新負荷的にいまいち実用的じゃない気がする。
            // 最適な敵数の範囲が異なるから
            // それよりは近距離の物理センサーで数秒に一回検査した方がいい。Nonalloc系のサーチでバッファに stack allocも使おう
            // 敵百体以上増やすならトリガーはまずいかも

            // 最も条件に近いターゲットを確認する。
            // 比較用初期値はInvertによって変動。
            TargetJudgeData targetJudgeData = brainData.behaviorSetting[selectMove].targetCondition;

            // 新しいターゲットのハッシュ
            int newTargetHash;

            // 状態変更の場合ここで戻る。
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.不要_状態変更 )
            {
                // 指定状態に移行
                resultData.result = CharacterController.BaseController.JudgeResult.状態を変更した;
                resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

                // 判断結果を設定。
                this.judgeResult[index] = resultData;
                return;
            }

            // それ以外であればターゲットを判断

            int tIndex = this.JudgeTargetByCondition(targetJudgeData, index);
            resultData.result = CharacterController.BaseController.JudgeResult.新しく判断をした;

            // ここでターゲット見つかってなければ待機に移行。
            if ( tIndex < 0 )
            {
                // 待機に移行
                resultData.actNum = (int)ActState.待機;
                //  Debug.Log($"ターゲット判断失敗　行動番号{selectMove}");
                this.judgeResult[index] = resultData;
                return;
            }

            newTargetHash = this._coldLog[tIndex].hashCode;

            resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;
            resultData.targetHash = newTargetHash;
            resultData.selectActCondition = selectMove;
            resultData.selectTargetCondition = (int)brainData.behaviorSetting[selectMove].targetCondition.judgeCondition;

            // 判断結果を設定。
            this.judgeResult[index] = resultData;

            // テスト仕様記録
            // 要素数は10 ～ 1000で
            // ステータスはいくつかベースとなるテンプレのCharacterData作って、その数値をいじるコード書いてやる。
            // で、Jobシステムをまんまベタ移植した普通のクラスを作成して、速度を比較
            // 最後は二つのテストにより作成されたpublic UnsafeList<MovementInfo> judgeResult　の同一性をかくにんして、精度のチェックまで終わり

            */
        }

        #region スキップ条件判断

        /// <summary>
        /// SkipJudgeConditionに基づいて判定を行うメソッド
        /// </summary>
        /// <param name="skipData">スキップ判定用データ</param>
        /// <param name="charaData">キャラクターデータ</param>
        /// <returns>条件に合致する場合は1、それ以外は0</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public int JudgeSkipByCondition(in SkipJudgeData skipData, int myIndex)
        {
            SkipJudgeCondition condition = skipData.skipCondition;
            switch ( condition )
            {
                case SkipJudgeCondition.自分のHPが一定割合の時:
                    // 各条件を個別に int で評価
                    int equalConditionHP = skipData.judgeValue == this._characterBaseInfo[myIndex].hpRatio ? 1 : 0;
                    int lessConditionHP = skipData.judgeValue < this._characterBaseInfo[myIndex].hpRatio ? 1 : 0;
                    int invertConditionHP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                    // 明示的に条件を組み合わせる
                    int condition1HP = equalConditionHP;
                    int condition2HP = lessConditionHP != 0 == (invertConditionHP != 0) ? 1 : 0;
                    if ( condition1HP != 0 || condition2HP != 0 )
                    {
                        return 1;
                    }

                    return 0;

                case SkipJudgeCondition.自分のMPが一定割合の時:
                    // 各条件を個別に int で評価
                    int equalConditionMP = skipData.judgeValue == this._characterBaseInfo[myIndex].mpRatio ? 1 : 0;
                    int lessConditionMP = skipData.judgeValue < this._characterBaseInfo[myIndex].mpRatio ? 1 : 0;
                    int invertConditionMP = skipData.isInvert == BitableBool.TRUE ? 1 : 0;
                    // 明示的に条件を組み合わせる
                    int condition1MP = equalConditionMP;
                    int condition2MP = lessConditionMP != 0 == (invertConditionMP != 0) ? 1 : 0;
                    if ( condition1MP != 0 || condition2MP != 0 )
                    {
                        return 1;
                    }

                    return 0;

                default:
                    // デフォルトケース（未定義の条件の場合）
                    Debug.LogWarning($"未定義のスキップ条件: {condition}");
                    return 0;
            }
        }

        #endregion スキップ条件判断

        /// <summary>
        /// 行動判断の処理を隔離したメソッド
        /// </summary>
        /// <param name="conditions"></param>
        /// <param name="charaData"></param>
        /// <param name="nowHate"></param>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public bool CheckActCondition(in ActJudgeData condition, int myIndex,
            int targetIndex)
        {
            bool result = true;

            // フィルター通過しないなら戻る。
            if ( condition.filter.IsPassFilter(this._solidData[targetIndex], this._characterStateInfo[targetIndex]) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                case ActJudgeCondition.指定のヘイト値の敵がいる時:

                    int targetHash = this._coldLog[targetIndex].hashCode;
                    int targetHate = 0;
                    int2 pHateKey = new(this._coldLog[myIndex].hashCode, targetHash);

                    if ( this.pHate.TryGetValue(pHateKey, out int hate) )
                    {
                        targetHate += hate;
                    }

                    // チームのヘイトはint2で確認する。
                    int2 hateKey = new((int)this._characterStateInfo[targetIndex].belong, targetHash);

                    if ( this.teamHate.TryGetValue(hateKey, out int tHate) )
                    {
                        targetHate += tHate;
                    }

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE ? targetHate >= condition.judgeValue : targetHate <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.HPが一定割合の対象がいる時:

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].hpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].hpRatio <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.MPが一定割合の対象がいる時:

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].mpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].mpRatio <= condition.judgeValue;

                    return result;

                case ActJudgeCondition.設定距離に対象がいる時:

                    // 二乗の距離で判定する。
                    int judgeDist = condition.judgeValue * condition.judgeValue;

                    // 今の距離の二乗。
                    int distance = (int)math.distancesq(this._characterBaseInfo[targetIndex].nowPosition, this._characterBaseInfo[targetIndex].nowPosition);

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE ? distance >= judgeDist : distance <= judgeDist;

                    return result;

                case ActJudgeCondition.特定の属性で攻撃する対象がいる時:

                    // 通常はいる時、逆の場合はいないとき
                    result = condition.isInvert == BitableBool.FALSE
                        ? ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) > 0
                        : ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) == 0;

                    return result;

                case ActJudgeCondition.特定の数の敵に狙われている時:
                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterStateInfo[targetIndex].targetingCount >= condition.judgeValue
                        : this._characterStateInfo[targetIndex].targetingCount <= condition.judgeValue;

                    return result;

                default: // 条件なし (0) または未定義の値
                    return result;
            }
        }

        #region　ターゲット判断処理

        /// <summary>
        /// TargetConditionに基づいて判定を行うメソッド
        /// </summary>
        /// <returns>返り値は行動ターゲットのインデックス</returns>
        // TargetConditionに基づいて判定を行うメソッド
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public int JudgeTargetByCondition(in TargetJudgeData judgeData, int myIndex)
        {

            int index = -1;

            TargetSelectCondition condition = judgeData.judgeCondition;

            int isInvert;
            int score;

            // 逆だから小さいのを探すので最大値入れる
            if ( judgeData.isInvert == BitableBool.TRUE )
            {
                isInvert = 1;
                score = int.MaxValue;
            }
            // 大きいのを探すので最小値スタート
            else
            {
                isInvert = 0;
                score = int.MinValue;
            }

            //if ( judgeData.judgeCondition == TargetSelectCondition.高度 && isInvert == 1 )
            //{
            //    Debug.Log($" 逆{judgeData.isInvert == BitableBool.TRUE} スコア初期{score}");
            //}

            switch ( condition )
            {
                case TargetSelectCondition.高度:
                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        int height = (int)this._characterBaseInfo[i].nowPosition.y;

                        // 一番高いキャラクターを求める (isInvert == 1)
                        if ( isInvert == 0 )
                        {
                            int isGreater = height > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = height;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める (isInvert == 0)
                        else
                        {
                            //   Debug.Log($" 番号{index} 高さ{score} 現在の高さ{height}　条件{height < score}");
                            int isLess = height < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = height;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.HP割合:
                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterBaseInfo[i].hpRatio > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterBaseInfo[i].hpRatio;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterBaseInfo[i].hpRatio < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterBaseInfo[i].hpRatio;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.HP:

                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterBaseInfo[i].currentHp > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterBaseInfo[i].currentHp;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterBaseInfo[i].currentHp < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterBaseInfo[i].currentHp;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.敵に狙われてる数:
                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterStateInfo[i].targetingCount > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterStateInfo[i].targetingCount;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterStateInfo[i].targetingCount < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterStateInfo[i].targetingCount;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.合計攻撃力:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].dispAtk > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].dispAtk;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterAtkStatus[i].dispAtk < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].dispAtk;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.合計防御力:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].dispDef > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].dispDef;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterDefStatus[i].dispDef < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].dispDef;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.斬撃攻撃力:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.slash > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.slash;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.slash < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.slash;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.刺突攻撃力:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.pierce > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.pierce;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.pierce < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.pierce;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.打撃攻撃力:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.strike > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.strike;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.strike < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.strike;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.炎攻撃力:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.fire > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.fire;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.fire < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.fire;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.雷攻撃力:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.lightning > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.lightning;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.lightning < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.lightning;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.光攻撃力:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.light > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.light;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.light < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.light;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.闇攻撃力:
                    for ( int i = 0; i < this._characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterAtkStatus[i].atk.dark > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.dark;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterAtkStatus[i].atk.dark < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterAtkStatus[i].atk.dark;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.斬撃防御力:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.slash > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.slash;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.slash < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.slash;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.刺突防御力:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.pierce > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.pierce;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.pierce < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.pierce;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.打撃防御力:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.strike > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.strike;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.strike < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.strike;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.炎防御力:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.fire > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.fire;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.fire < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.fire;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.雷防御力:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.lightning > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.lightning;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.lightning < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.lightning;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.光防御力:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.light > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.light;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.light < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.light;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.闇防御力:
                    for ( int i = 0; i < this._characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = this._characterDefStatus[i].def.dark > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = this._characterDefStatus[i].def.dark;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = this._characterDefStatus[i].def.dark < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = this._characterDefStatus[i].def.dark;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.距離:

                    // 自分の位置をキャッシュ
                    float2 myPosition = this._characterBaseInfo[myIndex].nowPosition;

                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // 自分自身か、フィルターをパスできなければ戻る。
                        if ( myIndex == i || judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }
                        // 2乗距離で遠近判断
                        // floatだから誤差が少し心配だね
                        float distance = Unity.Mathematics.math.distancesq(myPosition, this._characterBaseInfo[i].nowPosition);

                        // 一番高いキャラクターを求める。
                        if ( isInvert == 0 )
                        {
                            if ( distance > score )
                            {
                                score = (int)distance;
                                index = i;
                            }
                        }

                        // 一番低いキャラクターを求める。
                        else
                        {
                            if ( distance < score )
                            {
                                score = (int)distance;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.自分:
                    return myIndex;

                case TargetSelectCondition.プレイヤー:
                    // 何かしらのシングルトンにプレイヤーのHashは持たせとこ
                    // newTargetHash = characterData[i].hashCode;
                    return -1;

                case TargetSelectCondition.指定なし_ヘイト値:
                    // ターゲット選定ループ
                    for ( int i = 0; i < this._solidData.Length; i++ )
                    {
                        // 自分自身か、フィルターをパスできなければ戻る。
                        if ( i == index || judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }
                        // ヘイト値を確認
                        int targetHash = this._coldLog[i].hashCode;
                        int targetHate = 0;
                        int2 pHateKey = new(this._coldLog[myIndex].hashCode, targetHash);

                        if ( this.pHate.TryGetValue(pHateKey, out int hate) )
                        {
                            targetHate += hate;
                        }

                        // チームのヘイトはint2で確認する。
                        int2 hateKey = new((int)this._characterStateInfo[i].belong, targetHash);

                        if ( this.teamHate.TryGetValue(hateKey, out int tHate) )
                        {
                            targetHate += tHate;
                        }
                        // 一番高いキャラクターを求める。
                        if ( judgeData.isInvert == BitableBool.FALSE )
                        {
                            if ( targetHate > score )
                            {
                                score = targetHate;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める。
                        else
                        {
                            if ( targetHate < score )
                            {
                                score = targetHate;
                                index = i;
                            }
                        }
                    }

                    break;

                default:
                    // デフォルトケース（未定義の条件の場合）
                    Debug.LogWarning($"未定義のターゲット選択条件: {condition}");
                    return -1;
            }

            return -1;
        }

        #endregion ターゲット判断処理

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
    }

}
