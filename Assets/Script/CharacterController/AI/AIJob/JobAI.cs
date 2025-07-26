using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.BaseController;
using static CharacterController.StatusData.BrainStatus;
using static CharacterController.StatusData.BrainStatus.TriggerJudgeData;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace CharacterController
{
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
    public struct JobAI : IJobParallelFor
    {

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
        public UnsafeList<CharacterColdLog> _coldLog;

        /// <summary>
        /// 参照頻度の低いデータ
        /// </summary>
        [ReadOnly]
        public UnsafeList<RecognitionData> _recognizeData;

        /// <summary>
        /// 現在時間
        /// </summary>
        [ReadOnly]
        public float nowTime;

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
        /// キャラIDとモードからAIの設定をNativeArrayで抜き取れる。
        /// </summary>
        [ReadOnly]
        public BrainDataForJob brainArray;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="dataLists"></param>
        /// <param name="teamHate"></param>
        /// <param name="judgeResult"></param>
        /// <param name="relationMap"></param>
        /// <param name="brainArray"></param>
        /// <param name="nowTime"></param>
        public JobAI((
        UnsafeList<CharacterBaseInfo> characterBaseInfo,
        UnsafeList<CharacterAtkStatus> characterAtkStatus,
        UnsafeList<CharacterDefStatus> characterDefStatus,
        UnsafeList<SolidData> solidData,
        UnsafeList<CharacterStateInfo> characterStateInfo,
        UnsafeList<MoveStatus> moveStatus,
        UnsafeList<CharacterColdLog> coldLog,
            UnsafeList<RecognitionData> recognizeData
        ) dataLists, UnsafeList<CharacterController.BaseController.MovementInfo> judgeResult,
            NativeArray<int> relationMap, BrainDataForJob brainArray, float nowTime)
        {
            // タプルから各データリストを展開してフィールドに代入
            this._characterBaseInfo = dataLists.characterBaseInfo;
            this._characterAtkStatus = dataLists.characterAtkStatus;
            this._characterDefStatus = dataLists.characterDefStatus;
            this._solidData = dataLists.solidData;
            this._characterStateInfo = dataLists.characterStateInfo;
            this._moveStatus = dataLists.moveStatus;
            this._coldLog = dataLists.coldLog;
            this._recognizeData = dataLists.recognizeData;

            this.judgeResult = judgeResult;
            this.relationMap = relationMap;
            this.brainArray = brainArray;
            this.nowTime = nowTime;
        }

        /// <summary>
        /// characterDataとjudgeResultのインデックスをベースに処理する。
        /// </summary>
        /// <param name="index"></param>
        public void Execute(int index)
        {

            // 結果の構造体を作成。
            MovementInfo resultData = new();

            // 現在の行動のステートを数値に変換
            byte nowMode = this._coldLog[index].nowMode;

            // キャラのIDを取得
            byte characterID = this._coldLog[index].characterID;

            // 前回判断からの経過時間をまとめて取得
            // xがターゲット判断でyが行動判断、zが移動判断の経過時間。
            // wがトリガー判断の経過時間 
            float4 passTime = nowTime - this._coldLog[index].lastJudgeTime;

            // キャラの判断間隔をまとめて取得
            // xがターゲット判断でyが行動判断、zが移動判断の間隔。
            float3 judgeIntervals = this.brainArray.GetIntervalData(characterID, nowMode);

            // 変更を記録するフラグ
            // xがターゲット判断でyが行動判断、zが移動判断
            // wがモードチェンジ
            bool4 isJudged = new bool4(false, false, false, false);

            // 優先的に判断するターゲット条件の番号。
            // トリガーイベント等で指定がある。
            int priorityTargetCondition = -1;

            // 設定されたターゲットのハッシュコード
            int nextTargetIndex = -1;

            #region トリガーイベント判断

            // トリガー行動判断を行うか
            if ( passTime.w >= 0.5f )
            {
                NativeArray<TriggerJudgeData> triggerConditions = this.brainArray.GetTriggerJudgeDataArray(characterID, nowMode);


                // 条件を満たした行動の中で最も優先的なもの。
                // 初期値は-1、つまり何もトリガーされていない状態。
                int selectTrigger = -1;

                // 判断の必要がある条件をビットで保持
                int enableTriggerCondition = (1 << triggerConditions.Length) - 1;

                // キャラデータを確認する。
                for ( int i = 0; i < this._solidData.Length; i++ )
                {

                    // トリガー判断
                    if ( enableTriggerCondition != 0 )
                    {
                        for ( int j = 0; j < triggerConditions.Length - 1; j++ )
                        {
                            // ある条件満たしたらbreakして、以降はそれ以下の条件もう見ない。
                            if ( this.CheckTriggerCondition(triggerConditions[j], index, i) )
                            {
                                selectTrigger = j;

                                // enableConditionのbitも消す。
                                // i桁目までのビットをすべて1にするマスクを作成
                                // (1 << (i + 1)) - 1 は 0から i-1桁目までのビットがすべて1
                                int mask = (1 << j) - 1;

                                // マスクと元の値の論理積を取ることで上位ビットをクリア
                                enableTriggerCondition = enableTriggerCondition & mask;
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

                // 条件を満たしたトリガーがあればトリガーイベントを起こす
                if ( selectTrigger != -1 )
                {
                    switch ( triggerConditions[selectTrigger].triggerEventType )
                    {
                        case TriggerEventType.モード変更:
                            // モード変更の条件を満たした場合モードを変更する
                            isJudged.w = true;
                            nowMode = triggerConditions[selectTrigger].triggerNum;
                            break;
                        case TriggerEventType.ターゲット変更:
                            passTime.x = judgeIntervals.x + 1;// インターバルの時間以上の値を入れて判断するように

                            // 優先のターゲット条件を設定
                            priorityTargetCondition = triggerConditions[selectTrigger].triggerNum;
                            break;
                        case TriggerEventType.個別行動:
                            // 個別行動の条件を満たした場合
                            isJudged.y = true;
                            resultData.actNum = triggerConditions[selectTrigger].triggerNum;
                            break;
                    }
                }


                return;
            }

            #endregion トリガーイベント判断

            #region ターゲット判断

            // 時間経過かターゲット判断を行う状態で、ターゲット指定がされていなければ
            if ( (passTime.x >= judgeIntervals.x || (_characterStateInfo[index].actState & ActState.ターゲット変更) > 0)
                && (_characterStateInfo[index].brainEvent & AIManager.BrainEventFlagType.攻撃対象指定) == 0 )
            {
                // ターゲット条件を取得
                NativeArray<TargetJudgeData> targetConditions = this.brainArray.GetTargetJudgeDataArray(characterID, nowMode);

                // 優先的なターゲット条件が指定されている場合はそれを優先して判断する
                if ( priorityTargetCondition != -1 )
                {

                    // 判断後、優先条件は白紙に戻す
                    priorityTargetCondition = -1;
                }

                // 優先条件がない場合、あるいは優先でターゲットが見つからなかった場合は通常の判断を行う。
                if ( nextTargetIndex == -1 )
                {
                    for ( int i = 0; i < targetConditions.Length; i++ )
                    {
                        // 優先条件はすでに使ってるので飛ばす。
                        if ( i == priorityTargetCondition )
                        {
                            continue;
                        }
                    }
                }

                // もし-1の場合は自分をターゲットにする
                nextTargetIndex = nextTargetIndex == -1 ? index : nextTargetIndex;

                // 新ターゲットを設定。
                resultData.targetHash = _coldLog[nextTargetIndex].hashCode;
            }

            #endregion ターゲット判断

            #region 行動判断

            // 時間経過していて、かつトリガーイベントで行動設定がされてないなら
            if ( !isJudged.y && passTime.x >= judgeIntervals.y )
            {
                // クールタイムであるかのフラグ。
                bool isCoolTime = false;

                // クールタイム中ならクールタイムの判断を行う
                // 条件は行動につき一つ
                // ちなみにクールタイム中でもクールタイムじゃない行動はあるので判定自体はするよ。
                if ( passTime.y < this._coldLog[index].nowCoolTime.coolTime )
                {
                    // クールタイムのスキップ条件を満たしているかどうか
                    isCoolTime = (this.IsCoolTimeSkip(this._coldLog[index].nowCoolTime, index) == 0);
                }

                // 行動判断のデータを取得
                NativeArray<ActJudgeData> moveConditions = this.brainArray.GetActJudgeDataArray(characterID, nowMode);

                int selectMove = -1;

                for ( int i = 0; i < moveConditions.Length; i++ )
                {
                    // 実行可能性をクリアしたなら判断を実施
                    if ( moveConditions[i].actRatio == 100 || moveConditions[i].actRatio < GetRandomZeroToHandred() )
                    {
                        if ( IsActionConditionSatisfied(nextTargetIndex, moveConditions[i], isCoolTime) )
                        {
                            // クールタイムスキップ条件を満たしているので、行動を実行する。
                            selectMove = moveConditions[i].triggerNum;
                            isJudged.y = true;
                            break;
                        }
                    }
                }

                // 条件を満たした行動があれば行動を起こす
                if ( selectMove != -1 )
                {
                    switch ( moveConditions[selectMove].triggerEventType )
                    {
                        case TriggerEventType.モード変更:
                            // モード変更の条件を満たした場合モードを変更する
                            isJudged.w = true;
                            nowMode = moveConditions[selectMove].triggerNum;
                            isJudged.x = false; // ターゲット変更は行われていない
                            isJudged.z = false; // 移動判断は行われていない
                            isJudged.y = false; // 行動判断は行われていない。
                            break;
                        case TriggerEventType.ターゲット変更:
                            // 再度優先ターゲット条件を設定
                            priorityTargetCondition = moveConditions[selectMove].triggerNum;
                            isJudged.x = true; // ターゲット変更は行われた
                            isJudged.z = false; // 移動判断は行われていない
                            isJudged.y = false; // 行動判断は行われていない。
                            break;
                        case TriggerEventType.個別行動:
                            // 個別行動の条件を満たした場合
                            isJudged.y = true;
                            resultData.actNum = moveConditions[selectMove].triggerNum;
                            break;
                    }
                }
            }

            // 優先的なターゲット条件が指定されている場合はそれを優先して判断する
            if ( priorityTargetCondition != -1 )
            {

                // 判断後、優先条件は白紙に戻す
                priorityTargetCondition = -1;
            }

            #endregion 行動判断

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

        }

        #region クールタイムスキップ条件判断メソッド

        /// <summary>
        /// SkipJudgeConditionに基づいて判定を行うメソッド
        /// </summary>
        /// <param name="skipData">スキップ判定用データ</param>
        /// <param name="charaData">キャラクターデータ</param>
        /// <returns>条件に合致する場合は1、それ以外は0</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private byte IsCoolTimeSkip(in CoolTimeData skipData, int myIndex)
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

        #endregion クールタイムスキップ条件判断メソッド

        #region トリガーイベント判断メソッド

        /// <summary>
        /// トリガーイベント判断の処理を隔離したメソッド
        /// </summary>
        /// <param name="conditions"></param>
        /// <param name="charaData"></param>
        /// <param name="nowHate"></param>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckTriggerCondition(in TriggerJudgeData condition, int myIndex,
            int targetIndex)
        {
            bool result = true;

            // フィルター通過しないなら戻る。
            if ( condition.filter.IsPassFilter(this._solidData[targetIndex], this._characterStateInfo[targetIndex], this._characterBaseInfo[myIndex].nowPosition, this._characterBaseInfo[targetIndex].nowPosition) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                case ActTriggerCondition.指定のヘイト値の敵がいる時:

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

                case ActTriggerCondition.HPが一定割合の対象がいる時:

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].hpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].hpRatio <= condition.judgeValue;

                    return result;

                case ActTriggerCondition.MPが一定割合の対象がいる時:

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterBaseInfo[targetIndex].mpRatio >= condition.judgeValue
                        : this._characterBaseInfo[targetIndex].mpRatio <= condition.judgeValue;

                    return result;

                case ActTriggerCondition.設定距離に対象がいる時:

                    // 二乗の距離で判定する。
                    int judgeDist = condition.judgeValue * condition.judgeValue;

                    // 今の距離の二乗。
                    int distance = (int)math.distancesq(this._characterBaseInfo[myIndex].nowPosition, this._characterBaseInfo[targetIndex].nowPosition);

                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE ? distance >= judgeDist : distance <= judgeDist;

                    return result;

                case ActTriggerCondition.特定の属性で攻撃する対象がいる時:

                    // 通常はいる時、逆の場合はいないとき
                    result = condition.isInvert == BitableBool.FALSE
                        ? ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) > 0
                        : ((int)this._solidData[targetIndex].attackElement & condition.judgeValue) == 0;

                    return result;

                case ActTriggerCondition.特定の数の敵に狙われている時:
                    // 通常は以上、逆の場合は以下
                    result = condition.isInvert == BitableBool.FALSE
                        ? this._characterStateInfo[targetIndex].targetingCount >= condition.judgeValue
                        : this._characterStateInfo[targetIndex].targetingCount <= condition.judgeValue;

                    return result;

                default: // 条件なし (0) または未定義の値
                    return result;
            }
        }

        #endregion トリガーイベント判断メソッド

        #region　ターゲット判断処理

        /// <summary>
        /// TargetConditionに基づいて判定を行うメソッド
        /// </summary>
        /// <returns>返り値は行動ターゲットのインデックス</returns>
        // TargetConditionに基づいて判定を行うメソッド
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private int JudgeTargetByCondition(in TargetJudgeData judgeData, int myIndex)
        {

            int index = -1;

            // 自分の位置を取得
            float2 myPosition = this._characterBaseInfo[myIndex].nowPosition;

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



            switch ( condition )
            {
                case TargetSelectCondition.高度:
                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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
                        if ( judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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

                case TargetSelectCondition.自分:
                    return myIndex;

                case TargetSelectCondition.プレイヤー:
                    // 何かしらのシングルトンにプレイヤーのHashは持たせとこ
                    // newTargetHash = characterData[i].hashCode;
                    return -1;

                case TargetSelectCondition.指定なし_フィルターのみ:
                    // ターゲット選定ループ
                    for ( int i = 0; i < this._solidData.Length; i++ )
                    {
                        // 自分自身か、フィルターをパスできなければ戻る。
                        if ( i == index || judgeData.filter.IsPassFilter(this._solidData[i], this._characterStateInfo[i], myPosition, this._characterBaseInfo[i].nowPosition) == 0 )
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

        #region 行動判断メソッド

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool IsActionConditionSatisfied(int targetIndex, in ActJudgeData condition, bool isCoolTime)
        {
            // ターゲットが行動の条件を満たしているかを確認するメソッド。
            // ここでは、actNumが行動番号、targetIndexがターゲットのインデックス、judgeDataが判断データを表す。
            // 条件を満たしていればtrue、そうでなければfalseを返す。

        }

        #endregion 行動判断メソッド

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
        /// ゼロから百の中で乱数を生成するメソッド。
        /// </summary>
        /// <returns></returns>
        private int GetRandomZeroToHandred()
        {

        }

    }

}

