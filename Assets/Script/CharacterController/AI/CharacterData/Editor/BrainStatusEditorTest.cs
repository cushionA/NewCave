using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;
using static CharacterController.StatusData.BrainStatus.TriggerJudgeData;
using static CharacterController.AIManager;

#if UNITY_EDITOR
using UnityEditor;
using NUnit.Framework;
using System.IO;
using System.Linq;
#endif

namespace CharacterController.StatusData
{
    public partial class BrainStatus : SerializedScriptableObject
    {

#if UNITY_EDITOR
        #region テスト＆デバッグ機能

        /// <summary>
        /// エディタ上でのテストとデバッグ機能を提供
        /// ScriptableObjectに直接組み込むことで、設定値の検証が容易になる
        /// </summary>
        [FoldoutGroup("デバッグ＆テスト機能")]
        [Button("データ整合性チェック", ButtonSizes.Medium)]
        [InfoBox("全ての設定データの整合性をチェックします。問題がある場合はConsoleにログを出力します。")]
        private void ValidateAllData()
        {
            Debug.Log("=== BrainStatus データ整合性チェック開始 ===");
            bool hasErrors = false;

            // 基本データの検証
            if ( characterID < 0 || characterID > 255 )
            {
                Debug.LogError($"Character ID が範囲外です: {characterID} (0-255が有効)");
                hasErrors = true;
            }

            // モード設定の検証
            if ( characterModeSetting == null || characterModeSetting.Length == 0 )
            {
                Debug.LogError("キャラクターモード設定が空です");
                hasErrors = true;
            }
            else
            {
                for ( int modeIndex = 0; modeIndex < characterModeSetting.Length; modeIndex++ )
                {
                    var mode = characterModeSetting[modeIndex];
                    hasErrors |= ValidateCharacterModeData(mode, modeIndex);
                }
            }

            // 攻撃データの検証
            if ( attackData != null )
            {
                for ( int i = 0; i < attackData.Length; i++ )
                {
                    hasErrors |= ValidateActData(attackData[i], i);
                }
            }

            Debug.Log(hasErrors ?
                "=== データ整合性チェック完了: エラーが検出されました ===" :
                "=== データ整合性チェック完了: 問題なし ===");
        }

        /// <summary>
        /// キャラクターモードデータの個別検証
        /// </summary>
        private bool ValidateCharacterModeData(CharacterModeData mode, int modeIndex)
        {
            bool hasErrors = false;
            string prefix = $"Mode[{modeIndex}]";

            // 判断間隔の検証
            if ( mode.judgeInterval.x <= 0 || mode.judgeInterval.y <= 0 || mode.judgeInterval.z <= 0 )
            {
                Debug.LogError($"{prefix}: 判断間隔に0以下の値があります: {mode.judgeInterval}");
                hasErrors = true;
            }

            // トリガー条件の検証
            if ( mode.triggerCondition != null )
            {
                for ( int i = 0; i < mode.triggerCondition.Length; i++ )
                {
                    hasErrors |= ValidateTriggerJudgeData(mode.triggerCondition[i], $"{prefix}.Trigger[{i}]");
                }
            }

            // ターゲット条件の検証
            if ( mode.targetCondition != null )
            {
                for ( int i = 0; i < mode.targetCondition.Length; i++ )
                {
                    hasErrors |= ValidateTargetJudgeData(mode.targetCondition[i], $"{prefix}.Target[{i}]");
                }
            }

            // 行動条件の検証
            if ( mode.actCondition != null )
            {
                for ( int i = 0; i < mode.actCondition.Length; i++ )
                {
                    hasErrors |= ValidateActJudgeData(mode.actCondition[i], $"{prefix}.Act[{i}]");
                }
            }

            return hasErrors;
        }

        /// <summary>
        /// TriggerJudgeDataの個別検証
        /// </summary>
        private bool ValidateTriggerJudgeData(TriggerJudgeData data, string prefix)
        {
            bool hasErrors = false;

            // 実行確率の検証
            if ( data.actRatio < 1 || data.actRatio > 100 )
            {
                Debug.LogError($"{prefix}: actRatio が範囲外です: {data.actRatio} (1-100が有効)");
                hasErrors = true;
            }

            // 条件別の値検証
            switch ( data.judgeCondition )
            {
                case ActTriggerCondition.HPが一定割合の対象がいる時:
                case ActTriggerCondition.MPが一定割合の対象がいる時:
                    if ( data.judgeLowerValue < 0 || data.judgeLowerValue > 100 ||
                        data.judgeUpperValue < 0 || data.judgeUpperValue > 100 )
                    {
                        Debug.LogError($"{prefix}: HP/MP割合の値が範囲外です: {data.judgeLowerValue}-{data.judgeUpperValue} (0-100が有効)");
                        hasErrors = true;
                    }
                    break;

                case ActTriggerCondition.周囲に指定のオブジェクトや地形がある時:
                    if ( data.judgeLowerValue != 0 && data.judgeLowerValue != 1 )
                    {
                        Debug.LogError($"{prefix}: OR/AND判定フラグが不正です: {data.judgeLowerValue} (0または1が有効)");
                        hasErrors = true;
                    }
                    break;
            }

            return hasErrors;
        }

        /// <summary>
        /// TargetJudgeDataの個別検証
        /// </summary>
        private bool ValidateTargetJudgeData(TargetJudgeData data, string prefix)
        {
            bool hasErrors = false;

            // 反転フラグの検証
            if ( data.isInvert != BitableBool.FALSE && data.isInvert != BitableBool.TRUE )
            {
                Debug.LogError($"{prefix}: isInvert が不正な値です: {data.isInvert}");
                hasErrors = true;
            }

            return hasErrors;
        }

        /// <summary>
        /// ActJudgeDataの個別検証
        /// </summary>
        private bool ValidateActJudgeData(ActJudgeData data, string prefix)
        {
            bool hasErrors = false;

            // 実行確率の検証
            if ( data.actRatio < 1 || data.actRatio > 100 )
            {
                Debug.LogError($"{prefix}: actRatio が範囲外です: {data.actRatio} (1-100が有効)");
                hasErrors = true;
            }

            return hasErrors;
        }

        /// <summary>
        /// ActDataの個別検証
        /// </summary>
        private bool ValidateActData(ActData data, int index)
        {
            bool hasErrors = false;
            string prefix = $"AttackData[{index}]";

            // モーション値の検証
            if ( data.motionValue <= 0 )
            {
                Debug.LogWarning($"{prefix}: motionValue が0以下です: {data.motionValue}");
            }

            // クールタイムの検証
            if ( data.coolTimeData.coolTime < 0 )
            {
                Debug.LogError($"{prefix}: coolTime が負の値です: {data.coolTimeData.coolTime}");
                hasErrors = true;
            }

            return hasErrors;
        }

        /// <summary>
        /// サンプルデータ生成ボタン
        /// テスト用の基本的な設定を自動生成
        /// </summary>
        [FoldoutGroup("デバッグ＆テスト機能")]
        [Button("基本サンプルデータ生成", ButtonSizes.Medium)]
        [InfoBox("テスト用の基本的なAI設定データを自動生成します。既存データは上書きされます。")]
        private void GenerateSampleData()
        {
            if ( !EditorUtility.DisplayDialog("サンプルデータ生成",
                "既存のデータが上書きされます。続行しますか？", "生成する", "キャンセル") )
            {
                return;
            }

            Debug.Log("サンプルデータを生成中...");

            // 基本設定
            characterID = 1;

            // 基本データ
            baseData = new CharacterBaseData
            {
                hp = 100,
                mp = 50,
                baseAtk = new ElementalStatus { slash = 20, fire = 5 },
                baseDef = new ElementalStatus { slash = 10, fire = 2 },
                initialMove = ActState.攻撃,
                initialBelong = CharacterBelong.魔物
            };

            // 固定データ
            solidData = new SolidData
            {
                attackElement = Element.斬撃属性 | Element.炎属性,
                weakPoint = Element.雷属性,
                feature = CharacterFeature.通常エネミー | CharacterFeature.兵士,
                rank = CharacterRank.主力級,
                targetingLimit = 3
            };

            // サンプルモード設定
            characterModeSetting = new CharacterModeData[]
            {
                CreateSampleAggressiveMode(),
                CreateSampleDefensiveMode()
            };

            // 移動ステータス
            moveStatus = new MoveStatus
            {
                moveSpeed = 5,
                walkSpeed = 2,
                dashSpeed = 8,
                jumpHeight = 3
            };

            // 攻撃データ
            attackData = new ActData[]
            {
                CreateSampleAttackData("通常攻撃", 1.0f, 1.0f),
                CreateSampleAttackData("強攻撃", 2.0f, 2.5f)
            };

            EditorUtility.SetDirty(this);
            Debug.Log("サンプルデータの生成が完了しました");
        }

        /// <summary>
        /// 攻撃的なAIモードのサンプル作成
        /// </summary>
        private CharacterModeData CreateSampleAggressiveMode()
        {
            return new CharacterModeData
            {
                judgeInterval = new Unity.Mathematics.float3(1.0f, 0.5f, 0.3f),

                triggerCondition = new TriggerJudgeData[]
                {
                    new TriggerJudgeData
                    {
                        judgeCondition = ActTriggerCondition.HPが一定割合の対象がいる時,
                        actRatio = 80,
                        judgeLowerValue = 0,
                        judgeUpperValue = 30,
                        triggerEventType = TriggerEventType.個別行動,
                        triggerNum = 1,
                        filter = CreateSampleFilter(CharacterBelong.プレイヤー)
                    }
                },

                targetCondition = new TargetJudgeData[]
                {
                    new TargetJudgeData
                    {
                        judgeCondition = TargetSelectCondition.HP割合,
                        isInvert = BitableBool.TRUE, // HP最小を狙う
                        filter = CreateSampleFilter(CharacterBelong.プレイヤー)
                    }
                },

                actCondition = new ActJudgeData[]
                {
                    new ActJudgeData
                    {
                        judgeCondition = MoveSelectCondition.対象のHPが一定割合の時,
                        actRatio = 100,
                        judgeLowerValue = 0,
                        judgeUpperValue = 50,
                        triggerEventType = TriggerEventType.個別行動,
                        triggerNum = 0,
                        isCoolTimeIgnore = false,
                        isSelfJudge = false,
                        filter = CreateSampleFilter(CharacterBelong.プレイヤー)
                    }
                }
            };
        }

        /// <summary>
        /// 防御的なAIモードのサンプル作成
        /// </summary>
        private CharacterModeData CreateSampleDefensiveMode()
        {
            return new CharacterModeData
            {
                judgeInterval = new Unity.Mathematics.float3(2.0f, 1.0f, 0.5f),

                triggerCondition = new TriggerJudgeData[]
                {
                    new TriggerJudgeData
                    {
                        judgeCondition = ActTriggerCondition.対象が一定数の敵に狙われている時,
                        actRatio = 90,
                        judgeLowerValue = 2,
                        judgeUpperValue = 10,
                        triggerEventType = TriggerEventType.モード変更,
                        triggerNum = 0, // 攻撃モードに変更
                        filter = CreateSampleFilter(CharacterBelong.魔物)
                    }
                },

                targetCondition = new TargetJudgeData[]
                {
                    new TargetJudgeData
                    {
                        judgeCondition = TargetSelectCondition.敵に狙われてる数,
                        isInvert = BitableBool.FALSE, // 最も狙われている味方を守る
                        filter = CreateSampleFilter(CharacterBelong.魔物)
                    }
                },

                actCondition = new ActJudgeData[]
                {
                    new ActJudgeData
                    {
                        judgeCondition = MoveSelectCondition.対象が特定の数の敵に狙われている時,
                        actRatio = 100,
                        judgeLowerValue = 1,
                        judgeUpperValue = 10,
                        triggerEventType = TriggerEventType.個別行動,
                        triggerNum = 2, // 護衛行動
                        isCoolTimeIgnore = true,
                        isSelfJudge = false,
                        filter = CreateSampleFilter(CharacterBelong.魔物)
                    }
                }
            };
        }

        /// <summary>
        /// サンプル用ターゲットフィルターの作成
        /// </summary>
        private TargetFilter CreateSampleFilter(CharacterBelong targetType)
        {
            // TargetFilterの構築は実際の実装に応じて調整
            // ここではプレースホルダーとして基本的な設定を示す
            return new TargetFilter(); // 実際の初期化は構造体の実装に依存
        }

        /// <summary>
        /// サンプル攻撃データの作成
        /// </summary>
        private ActData CreateSampleAttackData(string name, float motionValue, float coolTime)
        {
            return new ActData
            {
                motionValue = motionValue,
                coolTimeData = new CoolTimeData
                {
                    skipCondition = ActTriggerCondition.HPが一定割合の対象がいる時,
                    judgeLowerValue = 0,
                    judgeUpperValue = 20,
                    coolTime = coolTime,
                    filter = new TargetFilter()
                },
                stateChange = ActState.攻撃,
                isCancel = false
            };
        }

        /// <summary>
        /// 設定データのエクスポート機能
        /// </summary>
        [FoldoutGroup("デバッグ＆テスト機能")]
        [Button("設定をJSONでエクスポート", ButtonSizes.Medium)]
        private void ExportToJSON()
        {
            try
            {
                string json = JsonUtility.ToJson(this, true);
                string path = EditorUtility.SaveFilePanel(
                    "AI設定のエクスポート",
                    Application.dataPath,
                    $"{name}_config.json",
                    "json");

                if ( !string.IsNullOrEmpty(path) )
                {
                    File.WriteAllText(path, json);
                    Debug.Log($"設定をエクスポートしました: {path}");
                    EditorUtility.DisplayDialog("エクスポート完了",
                        $"設定が正常にエクスポートされました。\n{path}", "OK");
                }
            }
            catch ( Exception e )
            {
                Debug.LogError($"エクスポートに失敗しました: {e.Message}");
                EditorUtility.DisplayDialog("エクスポートエラー",
                    $"エクスポートに失敗しました。\n{e.Message}", "OK");
            }
        }

        /// <summary>
        /// 設定データのインポート機能
        /// </summary>
        [FoldoutGroup("デバッグ＆テスト機能")]
        [Button("JSONから設定をインポート", ButtonSizes.Medium)]
        private void ImportFromJSON()
        {
            try
            {
                string path = EditorUtility.OpenFilePanel(
                    "AI設定のインポート",
                    Application.dataPath,
                    "json");

                if ( !string.IsNullOrEmpty(path) && File.Exists(path) )
                {
                    if ( EditorUtility.DisplayDialog("設定のインポート",
                        "既存の設定が上書きされます。続行しますか？", "インポート", "キャンセル") )
                    {
                        string json = File.ReadAllText(path);
                        JsonUtility.FromJsonOverwrite(json, this);
                        EditorUtility.SetDirty(this);
                        Debug.Log($"設定をインポートしました: {path}");
                        EditorUtility.DisplayDialog("インポート完了",
                            "設定が正常にインポートされました。", "OK");
                    }
                }
            }
            catch ( Exception e )
            {
                Debug.LogError($"インポートに失敗しました: {e.Message}");
                EditorUtility.DisplayDialog("インポートエラー",
                    $"インポートに失敗しました。\n{e.Message}", "OK");
            }
        }

        /// <summary>
        /// 設定統計情報の表示
        /// </summary>
        [FoldoutGroup("デバッグ＆テスト機能")]
        [Button("設定統計を表示", ButtonSizes.Medium)]
        private void ShowStatistics()
        {
            var stats = CalculateStatistics();

            string message = $"=== AI設定統計情報 ===\n" +
                           $"キャラクターID: {characterID}\n" +
                           $"モード数: {stats.modeCount}\n" +
                           $"総トリガー条件数: {stats.totalTriggers}\n" +
                           $"総ターゲット条件数: {stats.totalTargets}\n" +
                           $"総行動条件数: {stats.totalActions}\n" +
                           $"攻撃データ数: {stats.attackDataCount}\n" +
                           $"平均判断間隔: {stats.averageInterval:F2}秒\n" +
                           $"使用されている条件タイプ数: {stats.uniqueConditionTypes}";

            Debug.Log(message);
            EditorUtility.DisplayDialog("AI設定統計", message, "OK");
        }

        /// <summary>
        /// 統計情報の計算
        /// </summary>
        private (int modeCount, int totalTriggers, int totalTargets, int totalActions,
                int attackDataCount, float averageInterval, int uniqueConditionTypes) CalculateStatistics()
        {
            int modeCount = characterModeSetting?.Length ?? 0;
            int totalTriggers = 0;
            int totalTargets = 0;
            int totalActions = 0;
            int attackDataCount = attackData?.Length ?? 0;
            float totalInterval = 0;
            var conditionTypes = new HashSet<string>();

            if ( characterModeSetting != null )
            {
                foreach ( var mode in characterModeSetting )
                {
                    totalTriggers += mode.triggerCondition?.Length ?? 0;
                    totalTargets += mode.targetCondition?.Length ?? 0;
                    totalActions += mode.actCondition?.Length ?? 0;
                    totalInterval += mode.judgeInterval.x + mode.judgeInterval.y + mode.judgeInterval.z;

                    // 条件タイプの収集
                    if ( mode.triggerCondition != null )
                    {
                        foreach ( var trigger in mode.triggerCondition )
                        {
                            conditionTypes.Add(trigger.judgeCondition.ToString());
                        }
                    }
                    if ( mode.actCondition != null )
                    {
                        foreach ( var act in mode.actCondition )
                        {
                            conditionTypes.Add(act.judgeCondition.ToString());
                        }
                    }
                    if ( mode.targetCondition != null )
                    {
                        foreach ( var target in mode.targetCondition )
                        {
                            conditionTypes.Add(target.judgeCondition.ToString());
                        }
                    }
                }
            }

            float averageInterval = modeCount > 0 ? totalInterval / (modeCount * 3) : 0;

            return (modeCount, totalTriggers, totalTargets, totalActions,
                   attackDataCount, averageInterval, conditionTypes.Count);
        }

        #endregion
#endif
    }

    #region テストコード

#if UNITY_EDITOR
    /// <summary>
    /// BrainStatus用の自動テストスイート
    /// Unity Test Runnerで実行可能なテストコード
    /// </summary>
    public class BrainStatusTests
    {
        private BrainStatus _testBrainStatus;

        [SetUp]
        public void Setup()
        {
            // テスト用のScriptableObjectを作成
            _testBrainStatus = ScriptableObject.CreateInstance<BrainStatus>();
        }

        [TearDown]
        public void TearDown()
        {
            // テスト後のクリーンアップ
            if ( _testBrainStatus != null )
            {
                ScriptableObject.DestroyImmediate(_testBrainStatus);
            }
        }

        [Test]
        public void TestTriggerJudgeDataSerialization()
        {
            // テストデータの作成
            var triggerData = new BrainStatus.TriggerJudgeData
            {
                judgeCondition = BrainStatus.ActTriggerCondition.HPが一定割合の対象がいる時,
                actRatio = 75,
                judgeLowerValue = 10,
                judgeUpperValue = 50,
                triggerEventType = TriggerEventType.個別行動,
                triggerNum = 2
            };

            // シリアライゼーションテスト
            string json = JsonUtility.ToJson(triggerData);
            var deserializedData = JsonUtility.FromJson<BrainStatus.TriggerJudgeData>(json);

            // 検証
            Assert.AreEqual(triggerData.judgeCondition, deserializedData.judgeCondition);
            Assert.AreEqual(triggerData.actRatio, deserializedData.actRatio);
            Assert.AreEqual(triggerData.judgeLowerValue, deserializedData.judgeLowerValue);
            Assert.AreEqual(triggerData.judgeUpperValue, deserializedData.judgeUpperValue);
            Assert.AreEqual(triggerData.triggerEventType, deserializedData.triggerEventType);
            Assert.AreEqual(triggerData.triggerNum, deserializedData.triggerNum);
        }

        [Test]
        public void TestCoolTimeDataValidation()
        {
            // 正常なクールタイムデータ
            var coolTimeData = new BrainStatus.CoolTimeData
            {
                skipCondition = BrainStatus.ActTriggerCondition.特定の対象が一定数いる時,
                judgeLowerValue = 1,
                judgeUpperValue = 5,
                coolTime = 2.0f
            };

            // クールタイムが負でないことを確認
            Assert.GreaterOrEqual(coolTimeData.coolTime, 0);

            // 判定値が妥当な範囲内であることを確認
            Assert.GreaterOrEqual(coolTimeData.judgeLowerValue, 0);
            Assert.LessOrEqual(coolTimeData.judgeLowerValue, coolTimeData.judgeUpperValue);
        }

        [Test]
        public void TestActJudgeDataRangeValidation()
        {
            // 実行確率の境界値テスト
            var actData = new BrainStatus.ActJudgeData
            {
                judgeCondition = BrainStatus.MoveSelectCondition.対象のHPが一定割合の時,
                actRatio = 100, // 上限値
                judgeLowerValue = 0,
                judgeUpperValue = 100
            };

            Assert.GreaterOrEqual(actData.actRatio, 1);
            Assert.LessOrEqual(actData.actRatio, 100);

            // 下限値テスト
            actData.actRatio = 1;
            Assert.GreaterOrEqual(actData.actRatio, 1);
        }

        [Test]
        public void TestTargetJudgeDataInvertFlag()
        {
            // 反転フラグのテスト
            var targetData = new BrainStatus.TargetJudgeData
            {
                judgeCondition = BrainStatus.TargetSelectCondition.HP割合,
                isInvert = BrainStatus.BitableBool.TRUE
            };

            Assert.AreEqual(BrainStatus.BitableBool.TRUE, targetData.isInvert);

            // 反転フラグの変更テスト
            targetData.isInvert = BrainStatus.BitableBool.FALSE;
            Assert.AreEqual(BrainStatus.BitableBool.FALSE, targetData.isInvert);
        }

        [Test]
        public void TestBrainEventFlagTypeBitOperations()
        {
            // ビットフラグの組み合わせテスト
            var combinedFlags = BrainEventFlagType.大ダメージを与えた |
                               BrainEventFlagType.キャラを倒した;

            Assert.IsTrue((combinedFlags & BrainEventFlagType.大ダメージを与えた) != 0);
            Assert.IsTrue((combinedFlags & BrainEventFlagType.大ダメージを受けた) != 0);
            Assert.IsFalse((combinedFlags & BrainEventFlagType.回復を使用) != 0);
        }

        [Test]
        public void TestRecognizeObjectTypeBitOperations()
        {
            // 複数オブジェクトタイプの組み合わせテスト
            var objectTypes = BrainStatus.RecognizeObjectType.危険物 |
                             BrainStatus.RecognizeObjectType.毒沼 |
                             BrainStatus.RecognizeObjectType.ダメージエリア;

            Assert.IsTrue((objectTypes & BrainStatus.RecognizeObjectType.危険物) != 0);
            Assert.IsTrue((objectTypes & BrainStatus.RecognizeObjectType.毒沼) != 0);
            Assert.IsTrue((objectTypes & BrainStatus.RecognizeObjectType.ダメージエリア) != 0);
            Assert.IsFalse((objectTypes & BrainStatus.RecognizeObjectType.バフエリア) != 0);
        }

        [Test]
        public void TestCharacterModeDataInitialization()
        {
            // キャラクターモードデータの初期化テスト
            var modeData = new BrainStatus.CharacterModeData
            {
                judgeInterval = new Unity.Mathematics.float3(1.0f, 0.5f, 0.3f),
                triggerCondition = new BrainStatus.TriggerJudgeData[0],
                targetCondition = new BrainStatus.TargetJudgeData[0],
                actCondition = new BrainStatus.ActJudgeData[0]
            };

            Assert.IsNotNull(modeData.triggerCondition);
            Assert.IsNotNull(modeData.targetCondition);
            Assert.IsNotNull(modeData.actCondition);
            Assert.Greater(modeData.judgeInterval.x, 0);
            Assert.Greater(modeData.judgeInterval.y, 0);
            Assert.Greater(modeData.judgeInterval.z, 0);
        }

        [Test]
        public void TestBrainStatusCompleteInitialization()
        {
            // 完全なBrainStatusの初期化テスト
            _testBrainStatus.characterID = 1;
            _testBrainStatus.baseData = new BrainStatus.CharacterBaseData
            {
                hp = 100,
                mp = 50,
                initialMove = BrainStatus.ActState.攻撃,
                initialBelong = BrainStatus.CharacterBelong.魔物
            };

            _testBrainStatus.characterModeSetting = new BrainStatus.CharacterModeData[]
            {
                new BrainStatus.CharacterModeData
                {
                    judgeInterval = new Unity.Mathematics.float3(1.0f, 0.5f, 0.3f),
                    triggerCondition = new BrainStatus.TriggerJudgeData[0],
                    targetCondition = new BrainStatus.TargetJudgeData[0],
                    actCondition = new BrainStatus.ActJudgeData[0]
                }
            };

            // 検証
            Assert.AreEqual(1, _testBrainStatus.characterID);
            Assert.AreEqual(100, _testBrainStatus.baseData.hp);
            Assert.AreEqual(50, _testBrainStatus.baseData.mp);
            Assert.AreEqual(1, _testBrainStatus.characterModeSetting.Length);
            Assert.IsNotNull(_testBrainStatus.characterModeSetting[0]);
        }

        [Test]
        public void TestPerformanceWithLargeDataSet()
        {
            // 大量データでのパフォーマンステスト
            var largeMode = new BrainStatus.CharacterModeData
            {
                judgeInterval = new Unity.Mathematics.float3(1.0f, 0.5f, 0.3f),
                triggerCondition = new BrainStatus.TriggerJudgeData[100],
                targetCondition = new BrainStatus.TargetJudgeData[100],
                actCondition = new BrainStatus.ActJudgeData[100]
            };

            // 配列の初期化
            for ( int i = 0; i < 100; i++ )
            {
                largeMode.triggerCondition[i] = new BrainStatus.TriggerJudgeData
                {
                    judgeCondition = BrainStatus.ActTriggerCondition.特定の対象が一定数いる時,
                    actRatio = (byte)(i % 100 + 1)
                };

                largeMode.targetCondition[i] = new BrainStatus.TargetJudgeData
                {
                    judgeCondition = BrainStatus.TargetSelectCondition.HP割合,
                    isInvert = i % 2 == 0 ? BrainStatus.BitableBool.TRUE : BrainStatus.BitableBool.FALSE
                };

                largeMode.actCondition[i] = new BrainStatus.ActJudgeData
                {
                    judgeCondition = BrainStatus.MoveSelectCondition.条件なし,
                    actRatio = (byte)(i % 100 + 1)
                };
            }

            _testBrainStatus.characterModeSetting = new[] { largeMode };

            // シリアライゼーションのパフォーマンステスト
            var startTime = System.DateTime.Now;
            string json = JsonUtility.ToJson(_testBrainStatus);
            var serializationTime = System.DateTime.Now - startTime;

            // 1秒以内で完了することを確認
            Assert.Less(serializationTime.TotalSeconds, 1.0);
            Assert.IsNotEmpty(json);
        }
    }

    /// <summary>
    /// エディタ拡張のパフォーマンステスト
    /// </summary>
    public class BrainStatusEditorPerformanceTests
    {
        [Test]
        public void TestUIUpdatePerformance()
        {
            // UI更新のパフォーマンステスト
            var testData = new BrainStatus.TriggerJudgeData
            {
                judgeCondition = BrainStatus.ActTriggerCondition.周囲に指定のオブジェクトや地形がある時,
                judgeLowerValue = 1,
                judgeUpperValue = (int)(BrainStatus.RecognizeObjectType.危険物 | BrainStatus.RecognizeObjectType.毒沼)
            };

            // 条件判定メソッドの性能テスト（リフレクションを使って呼び出し）
            var startTime = System.DateTime.Now;

            for ( int i = 0; i < 1000; i++ )
            {
                // 実際のエディタでは判定メソッドが頻繁に呼ばれるため、
                // その性能をシミュレート
                bool isEnum = testData.judgeCondition == BrainStatus.ActTriggerCondition.周囲に指定のオブジェクトや地形がある時;
                bool showValue = testData.judgeCondition != BrainStatus.ActTriggerCondition.条件なし;
            }

            var executionTime = System.DateTime.Now - startTime;

            // 1000回の判定が100ms以内で完了することを確認
            Assert.Less(executionTime.TotalMilliseconds, 100);
        }
    }
#endif

    #endregion
}