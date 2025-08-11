using System;
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
using static MoreMountains.CorgiEngine.MyCharacter;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;
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
        public UnsafeList<MovementInfo> judgeResult;

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
            UnsafeList<RecognitionData> recognizeData,
            UnsafeList<MovementInfo> judgeResult
        ) dataLists,
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

            this.judgeResult = dataLists.judgeResult;
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

            // 乱数生成用のシードの初期化（インデックスと時間を組み合わせてシード値を生成）
            uint seed = SeedGenerate((uint)(characterID + index + (int)(nowTime * 1000)));

            #region トリガーイベント判断

            // トリガー行動判断を行う時間か
            // 一秒に一回だけ判定
            if ( passTime.w >= 1f )
            {
                NativeArray<TriggerJudgeData> triggerConditions = this.brainArray.GetTriggerJudgeDataArray(characterID, nowMode);

                // 条件を満たした行動の中で最も優先的なもの。
                // 初期値は-1、つまり何もトリガーされていない状態。
                int selectTrigger = -1;

                // 判断の必要がある条件をビットで保持
                int enableTriggerCondition = (1 << triggerConditions.Length) - 1;

                // 各イベントの実行確率を判断して、確率チェック失敗したものを除外
                for ( int i = 0; i < triggerConditions.Length; i++ )
                {
                    // 実行確率が100じゃなくて、かつ実行確率の範囲が乱数以下なら
                    if ( triggerConditions[i].actRatio != 100 && triggerConditions[i].actRatio < GetRandomValueZeroToHundred(ref seed) )
                    {
                        // i番目のビットを0にして判断対象から外す
                        enableTriggerCondition &= ~(1 << i);
                    }
                }

                // 最優先の条件のインデックスを保持する変数
                int mostPriorityTrigger = -1;

                // 有効な条件の中で最も順番が早い（優先度が高い）ものを取得
                for ( int i = 0; i < triggerConditions.Length; i++ )
                {
                    if ( (enableTriggerCondition & (1 << i)) != 0 )
                    {
                        mostPriorityTrigger = i;
                        break; // 最初に見つかったものが最優先なのでbreak
                    }
                }

                // カウントが必要な条件のために配列をセットアップ
                NativeArray<int> counterArray = new NativeArray<int>(triggerConditions.Length, Allocator.Temp);

                // カウンター配列の初期化
                for ( int i = 0; i < counterArray.Length; i++ )
                {
                    // i番目のビットが立っているかチェック
                    if ( (enableTriggerCondition & (1 << i)) == 0 )
                    {
                        counterArray[i] = -1;
                        continue; // このビットが0なら、実行確率で除外されているのでスキップ
                    }

                    // 集計が必要ならカウントを行う。
                    if ( triggerConditions[i].judgeCondition != ActTriggerCondition.条件なし
                        && triggerConditions[i].judgeCondition <= ActTriggerCondition.特定の対象が一定数いる時 )
                    {
                        counterArray[i] = 0; // カウントが必要な条件はカウント開始
                        continue;
                    }

                    // 集計は行わないなら-1
                    counterArray[i] = -1;

                }

                // キャラデータを確認するためのキャラ数分ループ
                for ( int i = mostPriorityTrigger; i < this._solidData.Length; i++ )
                {
                    // トリガー判断
                    if ( enableTriggerCondition != 0 )
                    {
                        // 各キャラに対し全条件を確認
                        for ( int j = 0; j < triggerConditions.Length - 1; j++ )
                        {

                            // j番目のビットが立っているかチェック
                            if ( (enableTriggerCondition & (1 << j)) == 0 )
                            {
                                continue; // このビットが0なら、実行確率で除外されているのでスキップ
                            }

                            // カウンター用変数を用意
                            int counterValue = counterArray[j];

                            // ある条件満たしたらbreakして、以降はそれ以下の条件もう見ない。
                            if ( this.JudgeTriggerCondition(triggerConditions[j], index, i, ref counterValue) )
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

                            counterArray[j] = counterValue; // カウンター値を更新
                        }
                    }

                    // 条件満たしたらループ終わり。
                    else
                    {
                        break;
                    }
                }

                // カウンターチェック
                for ( int i = 0; i < counterArray.Length; i++ )
                {
                    // 最優先条件が見つかったので集計中止
                    if ( selectTrigger == i )
                    {
                        break;
                    }

                    // カウントを満たしたなら
                    if ( counterArray[i] != -1 &&
                        (counterArray[i] >= triggerConditions[i].judgeLowerValue && counterArray[i] <= triggerConditions[i].judgeUpperValue) )
                    {
                        selectTrigger = i; // 条件を満たしたので選択
                        break;// 条件を満たしたのでループ終了
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

                // カウンター配列を解放
                counterArray.Dispose();
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
                    // ターゲット取得
                    nextTargetIndex = JudgeTargetCondition(targetConditions[priorityTargetCondition], index);

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
                    isCoolTime = (this.JudgeCoolTimeBreak(this._coldLog[index].nowCoolTime, index));
                }

                // 行動判断のデータを取得
                NativeArray<ActJudgeData> moveConditions = this.brainArray.GetActJudgeDataArray(characterID, nowMode);

                int selectMove = -1;

                for ( int i = 0; i < moveConditions.Length; i++ )
                {
                    // 実行可能性をクリアしたなら判断を実施
                    if ( (!isCoolTime || moveConditions[i].isCoolTimeIgnore)
                        && moveConditions[i].actRatio == 100 || moveConditions[i].actRatio <= GetRandomValueZeroToHundred(ref seed) )
                    {
                        if ( JudgeActCondition(nextTargetIndex, moveConditions[i], index, moveConditions[i].isSelfJudge) )
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
                // ターゲット取得
                nextTargetIndex = JudgeTargetCondition(this.brainArray.GetTargetJudgeDataArray(characterID, nowMode)[priorityTargetCondition], index);
            }

            #endregion 行動判断

            #region 移動判断（振り向き）

            // ターゲットがいる時、時間経過か行動が新規判断された場合
            if ( (nextTargetIndex != index && nextTargetIndex != -1) && (passTime.z >= judgeIntervals.z || isJudged.y) )
            {
                // 方向を取得
                int direction = this._characterBaseInfo[index].nowPosition.x < this._characterBaseInfo[nextTargetIndex].nowPosition.x ? 1 : -1;

                // ターゲットへの距離を設定
                resultData.targetDistance = direction * math.distance(this._characterBaseInfo[index].nowPosition, this._characterBaseInfo[nextTargetIndex].nowPosition);
                isJudged.z = true;
            }

            #endregion 移動判断（振り向き）

            // isJudgedは変更を記録するフラグ
            // xがターゲット判断でyが行動判断、zが移動判断
            // wがモードチェンジ

            // 判断結果を格納する。
            if ( isJudged.w )
            {
                resultData.result &= JudgeResult.モード変更した;
            }

            if ( isJudged.x )
            {
                resultData.result &= JudgeResult.ターゲット変更した;
            }

            if ( isJudged.y )
            {
                resultData.result &= JudgeResult.行動を変更した;
            }

            if ( isJudged.z )
            {
                resultData.result &= JudgeResult.方向を変更した;
            }

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
        /// <param name="condition">スキップ判定用データ</param>
        /// <param name="myIndex">キャラクターデータ</param>
        /// <returns>条件に合致する場合は1、それ以外は0</returns>
        private bool JudgeCoolTimeBreak(in CoolTimeData condition, int myIndex)
        {
            // 特定のターゲットが指定されていれば設定するための変数
            int targetIndex = -1;

            // フィルターチェック
            if ( condition.filter.SelfTarget )
            {
                targetIndex = myIndex; // 自分自身をターゲットにする
            }

            // プレイヤーはゼロがインデックス
            else if ( condition.filter.PlayerTarget )
            {
                targetIndex = 0;
            }

            // 自分のポジションは覚えておく
            float2 myPosition = this._characterBaseInfo[myIndex].nowPosition;

            // 特定の標的がなければ全キャラに対して確認。
            if ( targetIndex == -1 )
            {
                switch ( condition.skipCondition )
                {
                    // 特定の対象が一定数いる時
                    case ActTriggerCondition.特定の対象が一定数いる時:

                        int counter = 0;

                        // 全キャラ分確認。
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            if ( condition.filter.IsPassFilter(
                                this._solidData[i],
                                this._characterStateInfo[i],
                                myPosition,
                                this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // フィルターに合致しないのでスキップ
                            }
                            counter++;
                        }

                        // カウンターが条件を満たしているかチェック
                        return counter >= condition.judgeLowerValue && counter <= condition.judgeUpperValue;

                    // HPが一定割合の対象がいる時
                    case ActTriggerCondition.HPが一定割合の対象がいる時:

                        // 全キャラ分確認。
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            if ( condition.filter.IsPassFilter(
                                this._solidData[i],
                                this._characterStateInfo[i],
                                myPosition,
                                this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // フィルターに合致しないのでスキップ
                            }

                            int hpRatio = _characterBaseInfo[i].hpRatio;

                            if ( hpRatio >= condition.judgeLowerValue && hpRatio <= condition.judgeUpperValue )
                            {
                                return true; // 条件を満たしたキャラが見つかった
                            }
                        }

                        // 条件を満たすキャラが見つからなかった
                        return false;

                    // MPが一定割合の対象がいる時
                    case ActTriggerCondition.MPが一定割合の対象がいる時:

                        // 全キャラ分確認。
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            if ( condition.filter.IsPassFilter(
                                this._solidData[i],
                                this._characterStateInfo[i],
                                myPosition,
                                this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // フィルターに合致しないのでスキップ
                            }

                            int mpRatio = _characterBaseInfo[i].mpRatio;

                            if ( mpRatio >= condition.judgeLowerValue && mpRatio <= condition.judgeUpperValue )
                            {
                                return true; // 条件を満たしたキャラが見つかった
                            }
                        }

                        // 条件を満たすキャラが見つからなかった
                        return false;

                    // 対象のキャラが一定数以上密集している時
                    case ActTriggerCondition.対象のキャラの周囲に特定陣営が一定以上密集している時:
                        bool isMatch = false;

                        // 全キャラ分確認。
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {

                            if ( condition.filter.IsPassFilter(
                             this._solidData[i],
                             this._characterStateInfo[i],
                             myPosition,
                             this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // フィルターに合致しないのでスキップ
                            }

                            // 一個目の値を陣営に変換。
                            switch ( ((CharacterBelong)condition.judgeLowerValue) )
                            {
                                case CharacterBelong.プレイヤー:
                                    // プレイヤー陣営のキャラを数える
                                    isMatch = _recognizeData[i].nearlyPlayerSideCount >= condition.judgeUpperValue;
                                    break;
                                case CharacterBelong.魔物:
                                    // 魔物陣営のキャラを数える
                                    isMatch = _recognizeData[i].nearlyMonsterSideCount >= condition.judgeUpperValue;

                                    break;
                                case CharacterBelong.その他:
                                    // その他陣営のキャラを数える
                                    isMatch = _recognizeData[i].nearlyOtherSideCount >= condition.judgeUpperValue;
                                    break;
                            }

                            // 条件を満たしたキャラが見つかった
                            if ( isMatch )
                            {
                                return true;
                            }
                        }

                        // 条件を満たすキャラが見つからなかった
                        return false;

                    // 対象のキャラが一定以下しか密集していない時
                    case ActTriggerCondition.対象のキャラの周囲に特定陣営が一定以下しかいない時:

                        // 全キャラ分確認。
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            // 初期化
                            isMatch = false;

                            if ( condition.filter.IsPassFilter(
                             this._solidData[i],
                             this._characterStateInfo[i],
                             myPosition,
                             this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // フィルターに合致しないのでスキップ
                            }

                            // 一個目の値を陣営に変換。
                            switch ( ((CharacterBelong)condition.judgeLowerValue) )
                            {
                                case CharacterBelong.プレイヤー:
                                    // プレイヤー陣営のキャラを数える
                                    isMatch = _recognizeData[i].nearlyPlayerSideCount <= condition.judgeUpperValue;
                                    break;
                                case CharacterBelong.魔物:
                                    // 魔物陣営のキャラを数える
                                    isMatch = _recognizeData[i].nearlyMonsterSideCount <= condition.judgeUpperValue;

                                    break;
                                case CharacterBelong.その他:
                                    // その他陣営のキャラを数える
                                    isMatch = _recognizeData[i].nearlyOtherSideCount <= condition.judgeUpperValue;
                                    break;
                            }

                            // 条件を満たしたキャラが見つかった
                            if ( isMatch )
                            {
                                return true;
                            }
                        }

                        // 条件を満たすキャラが見つからなかった
                        return false;

                    // 周囲に指定のオブジェクトや地形がある時
                    case ActTriggerCondition.周囲に指定のオブジェクトや地形がある時:

                        // 全キャラ分確認。
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {

                            if ( condition.filter.IsPassFilter(
                             this._solidData[i],
                             this._characterStateInfo[i],
                             myPosition,
                             this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // フィルターに合致しないのでスキップ
                            }



                            // ここでは、judgeLowerValueがゼロなら or 判定
                            if ( condition.judgeLowerValue == 0 )
                            {
                                isMatch = (((int)_recognizeData[i].recognizeObject & condition.judgeUpperValue) > 0);
                            }

                            // and判定
                            else
                            {
                                isMatch = (((int)_recognizeData[i].recognizeObject & condition.judgeUpperValue) == condition.judgeUpperValue);
                            }


                            // 条件を満たしたキャラが見つかった
                            if ( isMatch )
                            {
                                return true;
                            }
                        }

                        // 条件を満たすキャラが見つからなかった
                        return false;

                    // 特定の数の敵に狙われている時
                    case ActTriggerCondition.対象が一定数の敵に狙われている時:

                        // 全キャラ分確認。
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            if ( condition.filter.IsPassFilter(
                                this._solidData[i],
                                this._characterStateInfo[i],
                                myPosition,
                                this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // フィルターに合致しないのでスキップ
                            }

                            // 対象のキャラの狙われている数を取得
                            int targetingCount = _characterStateInfo[targetIndex].targetingCount;

                            if ( targetingCount >= condition.judgeLowerValue && targetingCount <= condition.judgeUpperValue )
                            {
                                return true; // 条件を満たしたキャラが見つかった
                            }
                        }

                        // 条件を満たすキャラが見つからなかった
                        return false;

                    // 対象のキャラの一定距離以内に飛び道具がある時
                    case ActTriggerCondition.対象のキャラの一定距離以内に飛び道具がある時:

                        // 全キャラ分確認。
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {
                            if ( condition.filter.IsPassFilter(
                                this._solidData[i],
                                this._characterStateInfo[i],
                                myPosition,
                                this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // フィルターに合致しないのでスキップ
                            }

                            // 対象のキャラの飛び道具の検出距離を取得
                            float detectDistance = _recognizeData[targetIndex].detectNearestAttackDistance;
                            if ( detectDistance > 0 && detectDistance >= condition.judgeLowerValue && detectDistance <= condition.judgeUpperValue )
                            {
                                return true; // 条件を満たしたキャラが見つかった
                            }
                        }

                        // 条件を満たすキャラが見つからなかった
                        return false;

                    // 特定のイベントが発生した時
                    case ActTriggerCondition.特定のイベントが発生した時:

                        // 全キャラ分確認。
                        for ( int i = 0; i < _solidData.Length; i++ )
                        {

                            if ( condition.filter.IsPassFilter(
                             this._solidData[i],
                             this._characterStateInfo[i],
                             myPosition,
                             this._characterBaseInfo[i].nowPosition) == 0 )
                            {
                                continue; // フィルターに合致しないのでスキップ
                            }

                            // ここでは、judgeLowerValueがゼロなら or 判定
                            if ( condition.judgeLowerValue == 0 )
                            {
                                isMatch = (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) > 0);
                            }

                            // and判定
                            else
                            {
                                isMatch = (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) == condition.judgeUpperValue);
                            }

                            // 条件を満たしたキャラが見つかった
                            if ( isMatch )
                            {
                                return true;
                            }
                        }

                        // 条件を満たすキャラが見つからなかった
                        return false;
                }
            }

            // 特定の対象があれば単体に対して確認。
            else
            {
                // フィルターチェック
                if ( condition.filter.IsPassFilter(
                    this._solidData[targetIndex],
                    this._characterStateInfo[targetIndex],
                    this._characterBaseInfo[myIndex].nowPosition,
                    this._characterBaseInfo[targetIndex].nowPosition) == 0 )
                {
                    return false;
                }

                switch ( condition.skipCondition )
                {
                    // 特定の対象が一定数いる時
                    case ActTriggerCondition.特定の対象が一定数いる時:
                        return true;

                    // HPが一定割合の対象がいる時
                    case ActTriggerCondition.HPが一定割合の対象がいる時:
                        int hpRatio = _characterBaseInfo[targetIndex].hpRatio;
                        return hpRatio >= condition.judgeLowerValue && hpRatio <= condition.judgeUpperValue;

                    // MPが一定割合の対象がいる時
                    case ActTriggerCondition.MPが一定割合の対象がいる時:
                        int mpRatio = _characterBaseInfo[targetIndex].mpRatio;
                        return mpRatio >= condition.judgeLowerValue && mpRatio <= condition.judgeUpperValue;

                    // 対象のキャラが一定数以上密集している時
                    case ActTriggerCondition.対象のキャラの周囲に特定陣営が一定以上密集している時:

                        // 一個目の値を陣営に変換。
                        switch ( ((CharacterBelong)condition.judgeLowerValue) )
                        {
                            case CharacterBelong.プレイヤー:
                                // プレイヤー陣営のキャラを数える
                                return _recognizeData[targetIndex].nearlyPlayerSideCount >= condition.judgeUpperValue;
                            case CharacterBelong.魔物:
                                // 魔物陣営のキャラを数える
                                return _recognizeData[targetIndex].nearlyMonsterSideCount >= condition.judgeUpperValue;
                            case CharacterBelong.その他:
                                // その他陣営のキャラを数える
                                return _recognizeData[targetIndex].nearlyOtherSideCount >= condition.judgeUpperValue;
                        }

                        return false;

                    // 対象のキャラが一定数以下だけしかいない時
                    case ActTriggerCondition.対象のキャラの周囲に特定陣営が一定以下しかいない時:

                        // 一個目の値を陣営に変換。
                        switch ( ((CharacterBelong)condition.judgeLowerValue) )
                        {
                            case CharacterBelong.プレイヤー:
                                // プレイヤー陣営のキャラを数える
                                return _recognizeData[targetIndex].nearlyPlayerSideCount <= condition.judgeUpperValue;
                            case CharacterBelong.魔物:
                                // 魔物陣営のキャラを数える
                                return _recognizeData[targetIndex].nearlyMonsterSideCount <= condition.judgeUpperValue;
                            case CharacterBelong.その他:
                                // その他陣営のキャラを数える
                                return _recognizeData[targetIndex].nearlyOtherSideCount <= condition.judgeUpperValue;
                        }

                        return false;

                    // 周囲に指定のオブジェクトや地形がある時
                    case ActTriggerCondition.周囲に指定のオブジェクトや地形がある時:

                        // ここでは、judgeLowerValueがゼロなら or 判定
                        if ( condition.judgeLowerValue == 0 )
                        {
                            return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) > 0);
                        }

                        // and判定
                        else
                        {
                            return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) == condition.judgeUpperValue);
                        }

                    // 特定の数の敵に狙われている時
                    case ActTriggerCondition.対象が一定数の敵に狙われている時:
                        int targetingCount = _characterStateInfo[targetIndex].targetingCount;
                        return targetingCount >= condition.judgeLowerValue && targetingCount <= condition.judgeUpperValue;

                    // 対象のキャラの一定距離以内に飛び道具がある時
                    case ActTriggerCondition.対象のキャラの一定距離以内に飛び道具がある時:
                        float detectDistance = _recognizeData[targetIndex].detectNearestAttackDistance;
                        return detectDistance > 0 && detectDistance >= condition.judgeLowerValue && detectDistance <= condition.judgeUpperValue;

                    // 特定のイベントが発生した時
                    case ActTriggerCondition.特定のイベントが発生した時:

                        // ここでは、judgeLowerValueがゼロなら or 判定
                        if ( condition.judgeLowerValue == 0 )
                        {
                            return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) > 0);
                        }

                        // and判定
                        else
                        {
                            return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) == condition.judgeUpperValue);
                        }
                }
            }

            return false; // デフォルトは条件を満たさない
        }

        #endregion クールタイムスキップ条件判断メソッド

        #region トリガーイベント判断メソッド

        /// <summary>
        /// トリガーイベント判断の処理を隔離したメソッド
        /// </summary>
        /// <param name="conditions"></param>
        /// <param name="charaData"></param>
        /// <param name="nowHate"></param>
        private bool JudgeTriggerCondition(in TriggerJudgeData condition, int myIndex,
            int targetIndex, ref int counter)
        {
            // フィルターチェック
            if ( condition.filter.IsPassFilter(
                this._solidData[targetIndex],
                this._characterStateInfo[targetIndex],
                this._characterBaseInfo[myIndex].nowPosition,
                this._characterBaseInfo[targetIndex].nowPosition) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                // 特定の対象が一定数いる時
                case ActTriggerCondition.特定の対象が一定数いる時:
                    counter++;
                    return false;

                // HPが一定割合の対象がいる時
                case ActTriggerCondition.HPが一定割合の対象がいる時:
                    int hpRatio = _characterBaseInfo[targetIndex].hpRatio;
                    return hpRatio >= condition.judgeLowerValue && hpRatio <= condition.judgeUpperValue;

                // MPが一定割合の対象がいる時
                case ActTriggerCondition.MPが一定割合の対象がいる時:
                    int mpRatio = _characterBaseInfo[targetIndex].mpRatio;
                    return mpRatio >= condition.judgeLowerValue && mpRatio <= condition.judgeUpperValue;

                // 対象のキャラが一定数以上密集している時
                case ActTriggerCondition.対象のキャラの周囲に特定陣営が一定以上密集している時:

                    // 一個目の値を陣営に変換。
                    switch ( ((CharacterBelong)condition.judgeLowerValue) )
                    {
                        case CharacterBelong.プレイヤー:
                            // プレイヤー陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyPlayerSideCount >= condition.judgeUpperValue;
                        case CharacterBelong.魔物:
                            // 魔物陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyMonsterSideCount >= condition.judgeUpperValue;
                        case CharacterBelong.その他:
                            // その他陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyOtherSideCount >= condition.judgeUpperValue;
                    }

                    return false;

                // 対象のキャラが一定数以下だけしかいないしている時
                case ActTriggerCondition.対象のキャラの周囲に特定陣営が一定以下しかいない時:

                    // 一個目の値を陣営に変換。
                    switch ( ((CharacterBelong)condition.judgeLowerValue) )
                    {
                        case CharacterBelong.プレイヤー:
                            // プレイヤー陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyPlayerSideCount <= condition.judgeUpperValue;
                        case CharacterBelong.魔物:
                            // 魔物陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyMonsterSideCount <= condition.judgeUpperValue;
                        case CharacterBelong.その他:
                            // その他陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyOtherSideCount <= condition.judgeUpperValue;
                    }

                    return false;

                // 周囲に指定のオブジェクトや地形がある時
                case ActTriggerCondition.周囲に指定のオブジェクトや地形がある時:

                    // ここでは、judgeLowerValueがゼロなら or 判定
                    if ( condition.judgeLowerValue == 0 )
                    {
                        return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) > 0);
                    }

                    // and判定
                    else
                    {
                        return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) == condition.judgeUpperValue);
                    }

                // 特定の数の敵に狙われている時
                case ActTriggerCondition.対象が一定数の敵に狙われている時:
                    int targetingCount = _characterStateInfo[targetIndex].targetingCount;
                    return targetingCount >= condition.judgeLowerValue && targetingCount <= condition.judgeUpperValue;

                // 対象のキャラの一定距離以内に飛び道具がある時
                case ActTriggerCondition.対象のキャラの一定距離以内に飛び道具がある時:
                    float detectDistance = _recognizeData[targetIndex].detectNearestAttackDistance;
                    return detectDistance > 0 && detectDistance >= condition.judgeLowerValue && detectDistance <= condition.judgeUpperValue;

                // 特定のイベントが発生した時
                case ActTriggerCondition.特定のイベントが発生した時:

                    // ここでは、judgeLowerValueがゼロなら or 判定
                    if ( condition.judgeLowerValue == 0 )
                    {
                        return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) > 0);
                    }

                    // and判定
                    else
                    {
                        return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) == condition.judgeUpperValue);
                    }
            }

            return false; // デフォルトは条件を満たさない
        }

        #endregion トリガーイベント判断メソッド

        #region　ターゲット判断処理

        /// <summary>
        /// TargetConditionに基づいて判定を行うメソッド
        /// </summary>
        /// <returns>返り値は行動ターゲットのインデックス</returns>
        // TargetConditionに基づいて判定を行うメソッド
        private int JudgeTargetCondition(in TargetJudgeData judgeData, int myIndex)
        {
            int index = -1;
            float2 myPosition = this._characterBaseInfo[myIndex].nowPosition;

            TargetSelectCondition condition = judgeData.judgeCondition;
            bool isInvert = judgeData.isInvert == BitableBool.TRUE;
            int score = isInvert ? int.MaxValue : int.MinValue;

            // 特殊条件の処理（スコアベースではない条件）
            switch ( condition )
            {
                // 21. 自分
                case TargetSelectCondition.自分:
                    return myIndex;

                // 22. プレイヤー
                case TargetSelectCondition.プレイヤー:
                    return 0;

                // 23. シスターさん
                case TargetSelectCondition.シスターさん:
                    return _solidData.Length - 1;

                // 24-26. 密集人数系（特別処理）
                case TargetSelectCondition.プレイヤー陣営の密集人数:
                case TargetSelectCondition.魔物陣営の密集人数:
                case TargetSelectCondition.その他陣営の密集人数:
                    return FindMostDenseTarget(condition, judgeData.filter, isInvert, myIndex);

                // 27. 条件を満たす対象にとって最もヘイトが高いキャラ
                case TargetSelectCondition.条件を満たす対象にとって最もヘイトが高いキャラ:
                    return FindTargetWithHighestHate(judgeData.filter, myIndex);

                // 28. 条件を満たす対象に最後に攻撃したキャラ
                case TargetSelectCondition.条件を満たす対象に最後に攻撃したキャラ:
                    return FindLastAttacker(judgeData.filter, myIndex);

                // 29. 指定なし_フィルターのみ
                case TargetSelectCondition.指定なし_フィルターのみ:
                    // フィルター条件のみで最初に見つかった対象を返す
                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        if ( judgeData.filter.IsPassFilter(
                            _solidData[i],
                            _characterStateInfo[i],
                            myPosition,
                            _characterBaseInfo[i].nowPosition) != 0 )
                        {
                            return i;
                        }
                    }
                    return -1;

                // 1-20, 24-26. スコアベースの条件（GetTargetScoreで処理）
                case TargetSelectCondition.高度:
                case TargetSelectCondition.HP割合:
                case TargetSelectCondition.HP:
                case TargetSelectCondition.敵に狙われてる数:
                case TargetSelectCondition.合計攻撃力:
                case TargetSelectCondition.合計防御力:
                case TargetSelectCondition.斬撃攻撃力:
                case TargetSelectCondition.刺突攻撃力:
                case TargetSelectCondition.打撃攻撃力:
                case TargetSelectCondition.炎攻撃力:
                case TargetSelectCondition.雷攻撃力:
                case TargetSelectCondition.光攻撃力:
                case TargetSelectCondition.闇攻撃力:
                case TargetSelectCondition.斬撃防御力:
                case TargetSelectCondition.刺突防御力:
                case TargetSelectCondition.打撃防御力:
                case TargetSelectCondition.炎防御力:
                case TargetSelectCondition.雷防御力:
                case TargetSelectCondition.光防御力:
                case TargetSelectCondition.闇防御力:
                    // スコアベースの判断処理
                    for ( int i = 0; i < this._characterBaseInfo.Length; i++ )
                    {

                        // フィルターチェック
                        if ( judgeData.filter.IsPassFilter(
                            this._solidData[i],
                            this._characterStateInfo[i],
                            myPosition,
                            this._characterBaseInfo[i].nowPosition) == 0 )
                        {
                            continue;
                        }

                        // 条件に応じたスコア取得
                        int currentScore = GetTargetScore(condition, i);

                        // 最適なターゲットを更新（isInvertがtrueなら最小値、falseなら最大値）
                        if ( (isInvert && currentScore < score) || (!isInvert && currentScore > score) )
                        {
                            score = currentScore;
                            index = i;
                        }
                    }
                    break;

                default:
                    // すべての条件は上記でカバーされているため、ここには到達しない
                    break;
            }

            return index;
        }

        #region ターゲット判断ヘルパーメソッド

        /// <summary>
        /// 条件に応じたスコアを取得
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private int GetTargetScore(TargetSelectCondition condition, int targetIndex)
        {
            switch ( condition )
            {
                case TargetSelectCondition.高度:
                    return (int)_characterBaseInfo[targetIndex].nowPosition.y;

                case TargetSelectCondition.HP割合:
                    return _characterBaseInfo[targetIndex].hpRatio;

                case TargetSelectCondition.HP:
                    return _characterBaseInfo[targetIndex].currentHp;

                case TargetSelectCondition.敵に狙われてる数:
                    return _characterStateInfo[targetIndex].targetingCount;

                case TargetSelectCondition.合計攻撃力:
                    return _characterAtkStatus[targetIndex].dispAtk;

                case TargetSelectCondition.合計防御力:
                    return _characterDefStatus[targetIndex].dispDef;

                case TargetSelectCondition.斬撃攻撃力:
                    return _characterAtkStatus[targetIndex].atk.slash;

                case TargetSelectCondition.刺突攻撃力:
                    return _characterAtkStatus[targetIndex].atk.pierce;

                case TargetSelectCondition.打撃攻撃力:
                    return _characterAtkStatus[targetIndex].atk.strike;

                case TargetSelectCondition.炎攻撃力:
                    return _characterAtkStatus[targetIndex].atk.fire;

                case TargetSelectCondition.雷攻撃力:
                    return _characterAtkStatus[targetIndex].atk.lightning;

                case TargetSelectCondition.光攻撃力:
                    return _characterAtkStatus[targetIndex].atk.light;

                case TargetSelectCondition.闇攻撃力:
                    return _characterAtkStatus[targetIndex].atk.dark;

                case TargetSelectCondition.斬撃防御力:
                    return _characterDefStatus[targetIndex].def.slash;

                case TargetSelectCondition.刺突防御力:
                    return _characterDefStatus[targetIndex].def.pierce;

                case TargetSelectCondition.打撃防御力:
                    return _characterDefStatus[targetIndex].def.strike;

                case TargetSelectCondition.炎防御力:
                    return _characterDefStatus[targetIndex].def.fire;

                case TargetSelectCondition.雷防御力:
                    return _characterDefStatus[targetIndex].def.lightning;

                case TargetSelectCondition.光防御力:
                    return _characterDefStatus[targetIndex].def.light;

                case TargetSelectCondition.闇防御力:
                    return _characterDefStatus[targetIndex].def.dark;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// 最もヘイトが高いキャラクターを持つ対象を検索
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private int FindTargetWithHighestHate(in TargetFilter filter, int myIndex)
        {
            float2 myPosition = _characterBaseInfo[myIndex].nowPosition;
            int bestTargetIndex = -1;

            for ( int i = 0; i < _characterBaseInfo.Length; i++ )
            {

                if ( filter.IsPassFilter(
                    _solidData[i],
                    _characterStateInfo[i],
                    myPosition,
                    _characterBaseInfo[i].nowPosition) != 0 )
                {
                    // そのキャラクターが最もヘイトを持っている相手のハッシュ値
                    int hateTargetHash = _recognizeData[i].hateEnemyHash;
                    if ( hateTargetHash != 0 )
                    {
                        // ハッシュ値を持つキャラクターを全検索
                        for ( int j = 0; j < _coldLog.Length; j++ )
                        {
                            if ( _coldLog[j].hashCode == hateTargetHash )
                            {
                                bestTargetIndex = j;
                                break;
                            }
                        }
                    }
                }
            }

            return bestTargetIndex;
        }

        /// <summary>
        /// 最後に攻撃したキャラクターを検索
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private int FindLastAttacker(in TargetFilter filter, int myIndex)
        {
            float2 myPosition = _characterBaseInfo[myIndex].nowPosition;
            int bestIndex = -1;

            for ( int i = 0; i < _characterBaseInfo.Length; i++ )
            {

                if ( filter.IsPassFilter(
                    _solidData[i],
                    _characterStateInfo[i],
                    myPosition,
                    _characterBaseInfo[i].nowPosition) != 0 )
                {
                    // そのキャラクターを最後に攻撃した相手のハッシュ値
                    int attackerHash = _recognizeData[i].attackerHash;
                    if ( attackerHash != 0 )
                    {
                        // ハッシュ値を持つキャラクターを全検索
                        for ( int j = 0; j < _coldLog.Length; j++ )
                        {
                            if ( _coldLog[j].hashCode == attackerHash )
                            {
                                bestIndex = j;
                                break;
                            }
                        }
                    }
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// 最も密集しているターゲットを検索
        /// isInvert対応版
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private int FindMostDenseTarget(TargetSelectCondition condition, in TargetFilter filter, bool isInvert, int myIndex)
        {
            int bestIndex = -1;
            int bestScore = isInvert ? int.MaxValue : int.MinValue;
            float2 myPosition = _characterBaseInfo[myIndex].nowPosition;

            for ( int i = 0; i < _characterBaseInfo.Length; i++ )
            {
                if ( _coldLog[i].hashCode == 0 )
                    continue;

                if ( filter.IsPassFilter(
                    _solidData[i],
                    _characterStateInfo[i],
                    myPosition,
                    _characterBaseInfo[i].nowPosition) == 0 )
                {
                    continue;
                }

                int density = 0;
                RecognitionData recData = _recognizeData[i];

                switch ( condition )
                {
                    case TargetSelectCondition.プレイヤー陣営の密集人数:
                        density = recData.nearlyPlayerSideCount;
                        break;
                    case TargetSelectCondition.魔物陣営の密集人数:
                        density = recData.nearlyMonsterSideCount;
                        break;
                    case TargetSelectCondition.その他陣営の密集人数:
                        density = recData.nearlyOtherSideCount;
                        break;
                }

                // isInvertに応じて最大または最小を選択
                if ( (isInvert && density < bestScore) || (!isInvert && density > bestScore) )
                {
                    bestScore = density;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        #endregion ターゲット判断ヘルパーメソッド

        #endregion ターゲット判断処理

        #region 行動判断メソッド


        private bool JudgeActCondition(int targetIndex, in ActJudgeData condition, int myIndex, bool isSelfJudge)
        {
            // 自分自身を対象にする場合、targetIndexをmyIndexに置き換える
            targetIndex = isSelfJudge ? myIndex : targetIndex;

            // フィルターチェック
            // 対象がフィルターに当てはまる状態か
            if ( condition.filter.IsPassFilter(
                this._solidData[targetIndex],
                this._characterStateInfo[targetIndex],
                this._characterBaseInfo[myIndex].nowPosition,
                this._characterBaseInfo[targetIndex].nowPosition) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                // 1. 対象がフィルターに当てはまる時
                case MoveSelectCondition.対象がフィルターに当てはまる時:
                    return true; // フィルターチェックは既に通過

                // 2. 対象のHPが一定割合の時
                case MoveSelectCondition.対象のHPが一定割合の時:
                    int hpRatio = _characterBaseInfo[targetIndex].hpRatio;
                    return hpRatio >= condition.judgeLowerValue && hpRatio <= condition.judgeUpperValue;

                // 3. 対象のMPが一定割合の時
                case MoveSelectCondition.対象のMPが一定割合の時:
                    int mpRatio = _characterBaseInfo[targetIndex].mpRatio;
                    return mpRatio >= condition.judgeLowerValue && mpRatio <= condition.judgeUpperValue;

                // 4. 対象の周囲に特定陣営のキャラが一定以上密集している時
                case MoveSelectCondition.対象の周囲に特定陣営のキャラが一定以上密集している時:

                    // 一個目の値を陣営に変換。
                    switch ( ((CharacterBelong)condition.judgeLowerValue) )
                    {
                        case CharacterBelong.プレイヤー:
                            // プレイヤー陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyPlayerSideCount >= condition.judgeUpperValue;
                        case CharacterBelong.魔物:
                            // 魔物陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyMonsterSideCount >= condition.judgeUpperValue;
                        case CharacterBelong.その他:
                            // その他陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyOtherSideCount >= condition.judgeUpperValue;
                    }

                    return false;

                // 4. 対象の周囲に特定陣営のキャラが一定以下しかいない時
                case MoveSelectCondition.対象の周囲に特定陣営のキャラが一定以下しかいない時:

                    // 一個目の値を陣営に変換。
                    switch ( ((CharacterBelong)condition.judgeLowerValue) )
                    {
                        case CharacterBelong.プレイヤー:
                            // プレイヤー陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyPlayerSideCount <= condition.judgeUpperValue;
                        case CharacterBelong.魔物:
                            // 魔物陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyMonsterSideCount <= condition.judgeUpperValue;
                        case CharacterBelong.その他:
                            // その他陣営のキャラを数える
                            return _recognizeData[targetIndex].nearlyOtherSideCount <= condition.judgeUpperValue;
                    }

                    return false;

                // 5. 対象の周囲に指定のオブジェクトや地形がある時
                case MoveSelectCondition.対象の周囲に指定のオブジェクトや地形がある時:

                    // ここでは、judgeLowerValueがゼロなら or 判定
                    if ( condition.judgeLowerValue == 0 )
                    {
                        return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) > 0);
                    }

                    // and判定
                    else
                    {
                        return (((int)_recognizeData[targetIndex].recognizeObject & condition.judgeUpperValue) == condition.judgeUpperValue);
                    }

                // 6. 対象が特定の数の敵に狙われている時
                case MoveSelectCondition.対象が特定の数の敵に狙われている時:
                    int targetingCount = _characterStateInfo[targetIndex].targetingCount;
                    return targetingCount >= condition.judgeLowerValue && targetingCount <= condition.judgeUpperValue;

                // 8. 対象の一定距離以内に飛び道具がある時
                case MoveSelectCondition.対象の一定距離以内に飛び道具がある時:
                    float detectDistance = _recognizeData[targetIndex].detectNearestAttackDistance;
                    return detectDistance > 0 && detectDistance >= condition.judgeLowerValue && detectDistance <= condition.judgeUpperValue;

                // 9. 特定のイベントが発生した時
                case MoveSelectCondition.特定のイベントが発生した時:
                    // ここでは、judgeLowerValueがゼロなら or 判定
                    if ( condition.judgeLowerValue == 0 )
                    {
                        return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) > 0);
                    }

                    // and判定
                    else
                    {
                        return (((int)_characterStateInfo[targetIndex].brainEvent & condition.judgeUpperValue) == condition.judgeUpperValue);
                    }

                // 10. ターゲットが自分の場合
                case MoveSelectCondition.ターゲットが自分の場合:
                    return targetIndex == myIndex;

                // 11. 条件なし
                case MoveSelectCondition.条件なし:
                default:
                    return true;
            }
        }

        #region 行動判断ヘルパーメソッド

        /// <summary>
        /// 密集度チェック（特定キャラクター用）
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckDensity(int targetIndex, int lowerValue, int upperValue)
        {
            RecognitionData recData = _recognizeData[targetIndex];

            // 全陣営の合計密集人数
            int totalDensity = recData.nearlyPlayerSideCount +
                              recData.nearlyMonsterSideCount +
                              recData.nearlyOtherSideCount;

            return totalDensity >= lowerValue && totalDensity <= upperValue;
        }

        /// <summary>
        /// 密集度条件チェック（フィルター付き）
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckDensityCondition(in TargetFilter filter, int lowerValue, int upperValue, int myIndex)
        {
            // フィルター条件を満たすキャラクターの密集状況をチェック
            for ( int i = 0; i < _characterBaseInfo.Length; i++ )
            {
                if ( _coldLog[i].hashCode == 0 )
                    continue;

                if ( filter.IsPassFilter(
                    _solidData[i],
                    _characterStateInfo[i],
                    _characterBaseInfo[myIndex].nowPosition,
                    _characterBaseInfo[i].nowPosition) != 0 )
                {
                    if ( CheckDensity(i, lowerValue, upperValue) )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 近距離のターゲット数チェック
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckNearbyTargets(in TargetFilter filter, int lowerValue, int upperValue, int myIndex)
        {
            RecognitionData recData = _recognizeData[myIndex];
            CharacterBelong belongFilter = filter.GetTargetType();

            int nearbyCount = 0;
            if ( (belongFilter & CharacterBelong.プレイヤー) != 0 )
                nearbyCount += recData.nearlyPlayerSideCount;
            if ( (belongFilter & CharacterBelong.魔物) != 0 )
                nearbyCount += recData.nearlyMonsterSideCount;
            if ( (belongFilter & CharacterBelong.その他) != 0 )
                nearbyCount += recData.nearlyOtherSideCount;

            return nearbyCount >= lowerValue && nearbyCount <= upperValue;
        }

        /// <summary>
        /// 特定キャラクターの近距離ターゲット数チェック
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private bool CheckNearbyTargetsForCharacter(int targetIndex, int lowerValue, int upperValue)
        {
            RecognitionData recData = _recognizeData[targetIndex];

            int totalNearby = recData.nearlyPlayerSideCount +
                             recData.nearlyMonsterSideCount +
                             recData.nearlyOtherSideCount;

            return totalNearby >= lowerValue && totalNearby <= upperValue;
        }


        #endregion 行動判断ヘルパーメソッド

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
        /// シードの値を変更しつつ、その値を101で割った余りを返す。
        /// XorShift32アルゴリズムを使用…というかUnity.Mathmatics.Randomの実装と同じ
        /// 
        /// </summary>
        /// <returns>0-100の間で剰余を取ったランダム値</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private uint GetRandomValueZeroToHundred(ref uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed % 101;
        }

        /// <summary>
        /// 乱数生成の準備処理
        /// Unity.Mathmatics.Randomの実装と同じ方法でシードのビットを拡散したシードを作る処理
        /// </summary>
        /// <param name="seed">シード値のベース</param>
        /// <returns>ビットが拡散されたシード値</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint SeedGenerate(uint seed)
        {
            seed = (seed ^ 61u) ^ (seed >> 16);
            seed *= 9u;
            seed = seed ^ (seed >> 4);
            seed *= 0x27d4eb2du;
            seed = seed ^ (seed >> 15);

            return seed;
        }

    }

}

