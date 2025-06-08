using System;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static TestScript.Collections.SoACharaDataDic;
using static TestScript.SOATest.SOAStatus;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace TestScript
{
    /// <summary>
    /// AIが判断を行うJob
    /// 流れとしてはヘイト判断（ここで一番憎いヤツは出しておく）→行動判断→対象設定（攻撃/防御の場合ヘイト、それ以外の場合は任意条件を優先順に判断）
    /// ヘイト処理はチームヘイトが一番高いやつを陣営ごとに出しておいて、個人ヘイト足したらそれを超えるか、で見ていこうか
    /// UnsafeList<CharacterData> characterDataは論理削除で中身ないデータもあるからその判別もしないとな
    /// </summary>
    [BurstCompile]
    public struct SoAJob : IJobParallelFor
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
        /// </summary>
        public NativeArray<PersonalHateContainer> pHate;

        /// <summary>
        /// 現在時間
        /// </summary>
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
        /// 状態に基づいて最初にデータを一つだけ抜く。
        /// </summary>
        [ReadOnly]
        public NativeArray<BrainDataForJob> brainArray;

        /// <summary>
        /// characterDataとjudgeResultのインデックスをベースに処理する。
        /// </summary>
        /// <param name="index"></param>
        [BurstCompile]
        public void Execute(int index)
        {

            // 結果の構造体を作成。
            CharacterController.BaseController.MovementInfo resultData = new();

            // 現在の行動のステートを数値に変換
            int nowMode = (int)_characterStateInfo[index].actState;

            BrainSettingForJob brainData = brainArray[_coldLog[index].characterID - 1].brainSetting[nowMode];

            // インターバルをまとめて取得
            // xが行動、yが移動判断
            float2 intervals = brainArray[_coldLog[index].characterID - 1].GetInterval();

            // 判断時間が経過したかを確認。
            // 経過してないなら処理しない。
            // あるいはターゲット消えた場合も判定したい。チームヘイトに含まれてなければ。それだと味方がヘイトの時どうするの。
            // キャラ死亡時に全キャラに対しターゲットしてるかどうかを確認するようにしよう。で、ターゲットだったら前回判断時間をマイナスにする。
            if ( this.nowTime - this._coldLog[index].lastJudgeTime < intervals.x )
            {
                resultData.result = CharacterController.BaseController.JudgeResult.何もなし;

                // 移動方向判断だけはする。
                //　正確には距離判定。
                // ハッシュ値持ってんだからジョブから出た後でやろう。
                // Resultを解釈して

                // 結果を設定。
                this.judgeResult[index] = resultData;

                return;
            }

            // characterData[index].brainData[nowMode].judgeInterval みたいな値は何回も使うなら一時変数に保存していい。

            // まず判断時間の経過を確認
            // 次に線形探索で行動間隔の確認を行いつつ、敵にはヘイト判断も行う。
            // 全ての行動条件を確認しつつ、どの条件が確定したかを配列に入れる
            // ちなみに0、つまり一番優先度が高い設定が当てはまった場合は有無を言わさずループ中断。
            // 逆に何も当てはまらなかった場合は補欠条件が実行。
            // ちなみに逃走、とか支援、のモードをあんまり生かせてないよな。
            // モードごとに条件設定できるようにするか。
            // で、条件がいらないモードについては行動判断を省く

            // 確認対象の条件はビット値で保存。
            // そしてビットの設定がある条件のみ確認する。
            // 条件満たしたらビットは消す。
            // さらに、1とか2番目とかのより優先度が高い条件が付いたらそれ以下の条件は全部消す。
            // で、現段階で一番優先度が高い満たした条件を保存しておく
            // その状態で最後まで走査してヘイト値設定も完了する。
            // ちなみにヘイト値設定は自分がヘイト持ってる相手のヘイトを足した値を確認するだけだろ
            // ヘイト減少の仕組み考えないとな。30パーセントずつ減らす？　あーーーーーーーー

            // 行動条件の中で前提を満たしたものを取得するビット
            // なお、実際の判断時により優先的な条件が満たされた場合は上位ビットはまとめて消す。
            int enableCondition = 0;

            // 前提となる自分についてのスキップ条件を確認。
            // 最後の条件は補欠条件なので無視
            for ( int i = 0; i < brainData.behaviorSetting.Length - 1; i++ )
            {

                SkipJudgeData skipData = brainData.behaviorSetting[i].skipData;

                // スキップ条件を解釈して判断
                if ( skipData.skipCondition == SkipJudgeCondition.条件なし || JudgeSkipByCondition(skipData, index) == 1 )
                {
                    enableCondition |= 1 << i;
                }
            }

            // 条件を満たした行動の中で最も優先的なもの。
            // 初期値は最後の条件、つまり条件なしの補欠条件
            int selectMove = brainData.behaviorSetting.Length - 1;

            //// ヘイト条件確認用の一時バッファ
            //NativeArray<Vector2Int> hateIndex = new NativeArray<Vector2Int>(myData.brainData[nowMode].hateCondition.Length, Allocator.Temp);
            //NativeArray<TargetJudgeData> hateCondition = myData.brainData[nowMode].hateCondition;

            //// ヘイト確認バッファの初期化
            //for ( int i = 0; i < hateIndex.Length; i++ )
            //{
            //    if ( hateCondition[i].isInvert )
            //    {
            //        hateIndex[i].Set(int.MaxValue, -1);
            //    }
            //    else
            //    {
            //        hateIndex[i].Set(int.MinValue, -1);
            //    }
            //}

            // キャラデータを確認する。
            for ( int i = 0; i < _solidData.Length; i++ )
            {
                // 自分はスキップ
                if ( index == i )
                {
                    continue;
                }

                // 読み取り専用のNativeContainerへのアクセスを避けるためにヘイト系の処理は分離することに

                //// まずヘイト判断。
                //// 各ヘイト条件について、条件更新を記録する。
                //for ( int j = 0; j < hateCondition.Length; j++ )
                //{
                //    int value = hateIndex[j].x;
                //    if ( targetFunctions[(int)hateCondition[j].judgeCondition].Invoke(hateCondition[j], characterData[i], ref value) )
                //    {
                //        hateIndex[j].Set(value, i);
                //    }
                //}

                // 行動判断。
                // ここはスイッチ文使おう。連続するInt値ならコンパイラがジャンプテーブル作ってくれるので
                if ( enableCondition != 0 )
                {
                    for ( int j = 0; j < brainData.behaviorSetting.Length - 1; j++ )
                    {
                        // ある条件満たしたらbreakして、以降はそれ以下の条件もう見ない。
                        if ( CheckActCondition(brainData.behaviorSetting[j].actCondition, index, i) )
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

            //// ヘイト値の反映
            //for ( int i = 0; i < hateIndex.Length; i++ )
            //{
            //    int targetHate = 0;
            //    int targetHash = characterData[hateIndex[i].y].hashCode;

            //    if ( myData.personalHate.ContainsKey(targetHash) )
            //    {
            //        targetHate += (int)myData.personalHate[targetHash];
            //    }

            //    if ( teamHate[(int)myData.liveData.belong].ContainsKey(targetHash) )
            //    {
            //        targetHate += teamHate[(int)myData.liveData.belong][targetHash];
            //    }

            //    // 最低10は保証。
            //    targetHate = Math.Min(10,targetHate);

            //    int newHate = (int)(targetHate * hateCondition[i].useAttackOrHateNum);

            //}

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

            _ = targetJudgeData.isInvert == BitableBool.TRUE ? int.MaxValue : int.MinValue;
            int newTargetHash = 0;

            // 状態変更の場合ここで戻る。
            if ( targetJudgeData.judgeCondition == TargetSelectCondition.不要_状態変更 )
            {
                // 指定状態に移行
                resultData.result = CharacterController.BaseController.JudgeResult.新しく判断をした;
                resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;

                // 判断結果を設定。
                this.judgeResult[index] = resultData;
                return;
            }
            // それ以外であればターゲットを判断
            else
            {
                int tIndex = JudgeTargetByCondition(targetJudgeData, index);
                if ( tIndex >= 0 )
                {
                    newTargetHash = this._coldLog[tIndex].hashCode;

                    //   Debug.Log($"ターゲット判断成功:{tIndex}のやつ。  Hash：{newTargetHash}");
                }
                // ここでターゲット見つかってなければ待機に移行。
                else
                {
                    // 待機に移行
                    resultData.result = CharacterController.BaseController.JudgeResult.新しく判断をした;
                    resultData.actNum = (int)ActState.待機;
                    //  Debug.Log($"ターゲット判断失敗　行動番号{selectMove}");
                }
            }

            resultData.result = CharacterController.BaseController.JudgeResult.新しく判断をした;
            resultData.actNum = (int)targetJudgeData.useAttackOrHateNum;
            resultData.targetHash = newTargetHash;

            // 判断結果を設定。
            this.judgeResult[index] = resultData;

            // テスト仕様記録
            // 要素数は10 ～ 1000で
            // ステータスはいくつかベースとなるテンプレのCharacterData作って、その数値をいじるコード書いてやる。
            // で、Jobシステムをまんまベタ移植した普通のクラスを作成して、速度を比較
            // 最後は二つのテストにより作成されたpublic UnsafeList<MovementInfo> judgeResult　の同一性をかくにんして、精度のチェックまで終わり

        }

        #region スキップ条件判断

        /// <summary>
        /// SkipJudgeConditionに基づいて判定を行うメソッド
        /// </summary>
        /// <param name="skipData">スキップ判定用データ</param>
        /// <param name="charaData">キャラクターデータ</param>
        /// <returns>条件に合致する場合は1、それ以外は0</returns>
        [BurstCompile]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public int JudgeSkipByCondition(in SkipJudgeData skipData, int myIndex)
        {
            SkipJudgeCondition condition = skipData.skipCondition;
            switch ( condition )
            {
                case SkipJudgeCondition.自分のHPが一定割合の時:
                    // 各条件を個別に int で評価
                    int equalConditionHP = skipData.judgeValue == _characterBaseInfo[myIndex].hpRatio ? 1 : 0;
                    int lessConditionHP = skipData.judgeValue < _characterBaseInfo[myIndex].hpRatio ? 1 : 0;
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
                    int equalConditionMP = skipData.judgeValue == _characterBaseInfo[myIndex].mpRatio ? 1 : 0;
                    int lessConditionMP = skipData.judgeValue < _characterBaseInfo[myIndex].mpRatio ? 1 : 0;
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
        [BurstCompile]
        public bool CheckActCondition(in ActJudgeData condition, int myIndex,
            int targetIndex)
        {
            bool result = true;

            // フィルター通過しないなら戻る。
            if ( condition.filter.IsPassFilter(_solidData[targetIndex], _characterStateInfo[targetIndex]) == 0 )
            {
                return false;
            }

            switch ( condition.judgeCondition )
            {
                case ActJudgeCondition.指定のヘイト値の敵がいる時:

                    int targetHash = _coldLog[targetIndex].hashCode;
                    int targetHate = 0;

                    if ( pHate[myIndex].personalHate.TryGetValue(targetHash, out int hate) )
                    {
                        targetHate += hate;
                    }

                    // チームのヘイトはint2で確認する。
                    int2 hateKey = new((int)_characterStateInfo[targetIndex].belong, targetHash);

                    if ( teamHate.TryGetValue(hateKey, out int tHate) )
                    {
                        targetHate += tHate;
                    }

                    // 通常は以上、逆の場合は以下
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = targetHate >= condition.judgeValue;
                    }
                    else
                    {
                        result = targetHate <= condition.judgeValue;
                    }

                    return result;

                case ActJudgeCondition.HPが一定割合の対象がいる時:

                    // 通常は以上、逆の場合は以下
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = _characterBaseInfo[targetIndex].hpRatio >= condition.judgeValue;
                    }
                    else
                    {
                        result = _characterBaseInfo[targetIndex].hpRatio <= condition.judgeValue;
                    }

                    return result;

                case ActJudgeCondition.MPが一定割合の対象がいる時:

                    // 通常は以上、逆の場合は以下
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = _characterBaseInfo[targetIndex].mpRatio >= condition.judgeValue;
                    }
                    else
                    {
                        result = _characterBaseInfo[targetIndex].mpRatio <= condition.judgeValue;
                    }

                    return result;

                case ActJudgeCondition.設定距離に対象がいる時:

                    // 二乗の距離で判定する。
                    int judgeDist = condition.judgeValue * condition.judgeValue;

                    // 今の距離の二乗。
                    int distance = (int)(math.distancesq(_characterBaseInfo[targetIndex].nowPosition, _characterBaseInfo[targetIndex].nowPosition));

                    // 通常は以上、逆の場合は以下
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = distance >= judgeDist;
                    }
                    else
                    {
                        result = distance <= judgeDist;
                    }

                    return result;

                case ActJudgeCondition.特定の属性で攻撃する対象がいる時:

                    // 通常はいる時、逆の場合はいないとき
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = ((int)_solidData[targetIndex].attackElement & condition.judgeValue) > 0;
                    }
                    else
                    {
                        result = ((int)_solidData[targetIndex].attackElement & condition.judgeValue) == 0;
                    }

                    return result;

                case ActJudgeCondition.特定の数の敵に狙われている時:
                    // 通常は以上、逆の場合は以下
                    if ( condition.isInvert == BitableBool.FALSE )
                    {
                        result = _characterStateInfo[targetIndex].targetingCount >= condition.judgeValue;
                    }
                    else
                    {
                        result = _characterStateInfo[targetIndex].targetingCount <= condition.judgeValue;
                    }

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
        [BurstCompile]
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
                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        int height = (int)_characterBaseInfo[i].nowPosition.y;

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
                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterBaseInfo[i].hpRatio > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterBaseInfo[i].hpRatio;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterBaseInfo[i].hpRatio < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterBaseInfo[i].hpRatio;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.HP:

                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterBaseInfo[i].currentHp > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterBaseInfo[i].currentHp;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterBaseInfo[i].currentHp < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterBaseInfo[i].currentHp;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.敵に狙われてる数:
                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterStateInfo[i].targetingCount > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterStateInfo[i].targetingCount;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterStateInfo[i].targetingCount < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterStateInfo[i].targetingCount;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.合計攻撃力:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].dispAtk > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].dispAtk;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterAtkStatus[i].dispAtk < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].dispAtk;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.合計防御力:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].dispDef > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].dispDef;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterDefStatus[i].dispDef < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].dispDef;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.斬撃攻撃力:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.slash > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.slash;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.slash < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.slash;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.刺突攻撃力:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.pierce > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.pierce;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.pierce < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.pierce;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.打撃攻撃力:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.strike > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.strike;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.strike < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.strike;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.炎攻撃力:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.fire > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.fire;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.fire < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.fire;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.雷攻撃力:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.lightning > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.lightning;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.lightning < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.lightning;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.光攻撃力:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.light > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.light;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.light < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.light;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.闇攻撃力:
                    for ( int i = 0; i < _characterAtkStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterAtkStatus[i].atk.dark > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterAtkStatus[i].atk.dark;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterAtkStatus[i].atk.dark < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterAtkStatus[i].atk.dark;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.斬撃防御力:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.slash > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.slash;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterDefStatus[i].def.slash < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.slash;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.刺突防御力:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.pierce > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.pierce;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterDefStatus[i].def.pierce < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.pierce;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.打撃防御力:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.strike > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.strike;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterDefStatus[i].def.strike < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.strike;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.炎防御力:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.fire > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.fire;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterDefStatus[i].def.fire < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.fire;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.雷防御力:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.lightning > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.lightning;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterDefStatus[i].def.lightning < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.lightning;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.光防御力:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.light > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.light;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterDefStatus[i].def.light < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.light;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.闇防御力:
                    for ( int i = 0; i < _characterDefStatus.Length; i++ )
                    {
                        // フィルターをパスできなければ戻る。
                        if ( judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }

                        // 一番高いキャラクターを求める
                        if ( isInvert == 0 )
                        {
                            int isGreater = _characterDefStatus[i].def.dark > score ? 1 : 0;
                            if ( isGreater != 0 )
                            {
                                score = _characterDefStatus[i].def.dark;
                                index = i;
                            }
                        }
                        // 一番低いキャラクターを求める
                        else
                        {
                            int isLess = _characterDefStatus[i].def.dark < score ? 1 : 0;
                            if ( isLess != 0 )
                            {
                                score = _characterDefStatus[i].def.dark;
                                index = i;
                            }
                        }
                    }

                    return index;

                case TargetSelectCondition.距離:

                    // 自分の位置をキャッシュ
                    float2 myPosition = _characterBaseInfo[myIndex].nowPosition;

                    for ( int i = 0; i < _characterBaseInfo.Length; i++ )
                    {
                        // 自分自身か、フィルターをパスできなければ戻る。
                        if ( myIndex == i || judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }
                        // 2乗距離で遠近判断
                        // floatだから誤差が少し心配だね
                        float distance = Unity.Mathematics.math.distancesq(myPosition, _characterBaseInfo[i].nowPosition);

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
                    for ( int i = 0; i < _solidData.Length; i++ )
                    {
                        // 自分自身か、フィルターをパスできなければ戻る。
                        if ( i == index || judgeData.filter.IsPassFilter(_solidData[i], _characterStateInfo[i]) == 0 )
                        {
                            continue;
                        }
                        // ヘイト値を確認
                        int targetHash = _coldLog[i].hashCode;
                        int targetHate = 0;

                        if ( pHate[myIndex].personalHate.TryGetValue(targetHash, out int hate) )
                        {
                            targetHate += hate;
                        }

                        // チームのヘイトはint2で確認する。
                        int2 hateKey = new((int)_characterStateInfo[i].belong, targetHash);

                        if ( teamHate.TryGetValue(hateKey, out int tHate) )
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

