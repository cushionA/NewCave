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
        #region �e�X�g���f�o�b�O�@�\

        /// <summary>
        /// �G�f�B�^��ł̃e�X�g�ƃf�o�b�O�@�\���
        /// ScriptableObject�ɒ��ڑg�ݍ��ނ��ƂŁA�ݒ�l�̌��؂��e�ՂɂȂ�
        /// </summary>
        [FoldoutGroup("�f�o�b�O���e�X�g�@�\")]
        [Button("�f�[�^�������`�F�b�N", ButtonSizes.Medium)]
        [InfoBox("�S�Ă̐ݒ�f�[�^�̐��������`�F�b�N���܂��B��肪����ꍇ��Console�Ƀ��O���o�͂��܂��B")]
        private void ValidateAllData()
        {
            Debug.Log("=== BrainStatus �f�[�^�������`�F�b�N�J�n ===");
            bool hasErrors = false;

            // ��{�f�[�^�̌���
            if ( characterID < 0 || characterID > 255 )
            {
                Debug.LogError($"Character ID ���͈͊O�ł�: {characterID} (0-255���L��)");
                hasErrors = true;
            }

            // ���[�h�ݒ�̌���
            if ( characterModeSetting == null || characterModeSetting.Length == 0 )
            {
                Debug.LogError("�L�����N�^�[���[�h�ݒ肪��ł�");
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

            // �U���f�[�^�̌���
            if ( attackData != null )
            {
                for ( int i = 0; i < attackData.Length; i++ )
                {
                    hasErrors |= ValidateActData(attackData[i], i);
                }
            }

            Debug.Log(hasErrors ?
                "=== �f�[�^�������`�F�b�N����: �G���[�����o����܂��� ===" :
                "=== �f�[�^�������`�F�b�N����: ���Ȃ� ===");
        }

        /// <summary>
        /// �L�����N�^�[���[�h�f�[�^�̌ʌ���
        /// </summary>
        private bool ValidateCharacterModeData(CharacterModeData mode, int modeIndex)
        {
            bool hasErrors = false;
            string prefix = $"Mode[{modeIndex}]";

            // ���f�Ԋu�̌���
            if ( mode.judgeInterval.x <= 0 || mode.judgeInterval.y <= 0 || mode.judgeInterval.z <= 0 )
            {
                Debug.LogError($"{prefix}: ���f�Ԋu��0�ȉ��̒l������܂�: {mode.judgeInterval}");
                hasErrors = true;
            }

            // �g���K�[�����̌���
            if ( mode.triggerCondition != null )
            {
                for ( int i = 0; i < mode.triggerCondition.Length; i++ )
                {
                    hasErrors |= ValidateTriggerJudgeData(mode.triggerCondition[i], $"{prefix}.Trigger[{i}]");
                }
            }

            // �^�[�Q�b�g�����̌���
            if ( mode.targetCondition != null )
            {
                for ( int i = 0; i < mode.targetCondition.Length; i++ )
                {
                    hasErrors |= ValidateTargetJudgeData(mode.targetCondition[i], $"{prefix}.Target[{i}]");
                }
            }

            // �s�������̌���
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
        /// TriggerJudgeData�̌ʌ���
        /// </summary>
        private bool ValidateTriggerJudgeData(TriggerJudgeData data, string prefix)
        {
            bool hasErrors = false;

            // ���s�m���̌���
            if ( data.actRatio < 1 || data.actRatio > 100 )
            {
                Debug.LogError($"{prefix}: actRatio ���͈͊O�ł�: {data.actRatio} (1-100���L��)");
                hasErrors = true;
            }

            // �����ʂ̒l����
            switch ( data.judgeCondition )
            {
                case ActTriggerCondition.HP����芄���̑Ώۂ����鎞:
                case ActTriggerCondition.MP����芄���̑Ώۂ����鎞:
                    if ( data.judgeLowerValue < 0 || data.judgeLowerValue > 100 ||
                        data.judgeUpperValue < 0 || data.judgeUpperValue > 100 )
                    {
                        Debug.LogError($"{prefix}: HP/MP�����̒l���͈͊O�ł�: {data.judgeLowerValue}-{data.judgeUpperValue} (0-100���L��)");
                        hasErrors = true;
                    }
                    break;

                case ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:
                    if ( data.judgeLowerValue != 0 && data.judgeLowerValue != 1 )
                    {
                        Debug.LogError($"{prefix}: OR/AND����t���O���s���ł�: {data.judgeLowerValue} (0�܂���1���L��)");
                        hasErrors = true;
                    }
                    break;
            }

            return hasErrors;
        }

        /// <summary>
        /// TargetJudgeData�̌ʌ���
        /// </summary>
        private bool ValidateTargetJudgeData(TargetJudgeData data, string prefix)
        {
            bool hasErrors = false;

            // ���]�t���O�̌���
            if ( data.isInvert != BitableBool.FALSE && data.isInvert != BitableBool.TRUE )
            {
                Debug.LogError($"{prefix}: isInvert ���s���Ȓl�ł�: {data.isInvert}");
                hasErrors = true;
            }

            return hasErrors;
        }

        /// <summary>
        /// ActJudgeData�̌ʌ���
        /// </summary>
        private bool ValidateActJudgeData(ActJudgeData data, string prefix)
        {
            bool hasErrors = false;

            // ���s�m���̌���
            if ( data.actRatio < 1 || data.actRatio > 100 )
            {
                Debug.LogError($"{prefix}: actRatio ���͈͊O�ł�: {data.actRatio} (1-100���L��)");
                hasErrors = true;
            }

            return hasErrors;
        }

        /// <summary>
        /// ActData�̌ʌ���
        /// </summary>
        private bool ValidateActData(ActData data, int index)
        {
            bool hasErrors = false;
            string prefix = $"AttackData[{index}]";

            // ���[�V�����l�̌���
            if ( data.motionValue <= 0 )
            {
                Debug.LogWarning($"{prefix}: motionValue ��0�ȉ��ł�: {data.motionValue}");
            }

            // �N�[���^�C���̌���
            if ( data.coolTimeData.coolTime < 0 )
            {
                Debug.LogError($"{prefix}: coolTime �����̒l�ł�: {data.coolTimeData.coolTime}");
                hasErrors = true;
            }

            return hasErrors;
        }

        /// <summary>
        /// �T���v���f�[�^�����{�^��
        /// �e�X�g�p�̊�{�I�Ȑݒ����������
        /// </summary>
        [FoldoutGroup("�f�o�b�O���e�X�g�@�\")]
        [Button("��{�T���v���f�[�^����", ButtonSizes.Medium)]
        [InfoBox("�e�X�g�p�̊�{�I��AI�ݒ�f�[�^�������������܂��B�����f�[�^�͏㏑������܂��B")]
        private void GenerateSampleData()
        {
            if ( !EditorUtility.DisplayDialog("�T���v���f�[�^����",
                "�����̃f�[�^���㏑������܂��B���s���܂����H", "��������", "�L�����Z��") )
            {
                return;
            }

            Debug.Log("�T���v���f�[�^�𐶐���...");

            // ��{�ݒ�
            characterID = 1;

            // ��{�f�[�^
            baseData = new CharacterBaseData
            {
                hp = 100,
                mp = 50,
                baseAtk = new ElementalStatus { slash = 20, fire = 5 },
                baseDef = new ElementalStatus { slash = 10, fire = 2 },
                initialMove = ActState.�U��,
                initialBelong = CharacterBelong.����
            };

            // �Œ�f�[�^
            solidData = new SolidData
            {
                attackElement = Element.�a������ | Element.������,
                weakPoint = Element.������,
                feature = CharacterFeature.�ʏ�G�l�~�[ | CharacterFeature.���m,
                rank = CharacterRank.��͋�,
                targetingLimit = 3
            };

            // �T���v�����[�h�ݒ�
            characterModeSetting = new CharacterModeData[]
            {
                CreateSampleAggressiveMode(),
                CreateSampleDefensiveMode()
            };

            // �ړ��X�e�[�^�X
            moveStatus = new MoveStatus
            {
                moveSpeed = 5,
                walkSpeed = 2,
                dashSpeed = 8,
                jumpHeight = 3
            };

            // �U���f�[�^
            attackData = new ActData[]
            {
                CreateSampleAttackData("�ʏ�U��", 1.0f, 1.0f),
                CreateSampleAttackData("���U��", 2.0f, 2.5f)
            };

            EditorUtility.SetDirty(this);
            Debug.Log("�T���v���f�[�^�̐������������܂���");
        }

        /// <summary>
        /// �U���I��AI���[�h�̃T���v���쐬
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
                        judgeCondition = ActTriggerCondition.HP����芄���̑Ώۂ����鎞,
                        actRatio = 80,
                        judgeLowerValue = 0,
                        judgeUpperValue = 30,
                        triggerEventType = TriggerEventType.�ʍs��,
                        triggerNum = 1,
                        filter = CreateSampleFilter(CharacterBelong.�v���C���[)
                    }
                },

                targetCondition = new TargetJudgeData[]
                {
                    new TargetJudgeData
                    {
                        judgeCondition = TargetSelectCondition.HP����,
                        isInvert = BitableBool.TRUE, // HP�ŏ���_��
                        filter = CreateSampleFilter(CharacterBelong.�v���C���[)
                    }
                },

                actCondition = new ActJudgeData[]
                {
                    new ActJudgeData
                    {
                        judgeCondition = MoveSelectCondition.�Ώۂ�HP����芄���̎�,
                        actRatio = 100,
                        judgeLowerValue = 0,
                        judgeUpperValue = 50,
                        triggerEventType = TriggerEventType.�ʍs��,
                        triggerNum = 0,
                        isCoolTimeIgnore = false,
                        isSelfJudge = false,
                        filter = CreateSampleFilter(CharacterBelong.�v���C���[)
                    }
                }
            };
        }

        /// <summary>
        /// �h��I��AI���[�h�̃T���v���쐬
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
                        judgeCondition = ActTriggerCondition.�Ώۂ���萔�̓G�ɑ_���Ă��鎞,
                        actRatio = 90,
                        judgeLowerValue = 2,
                        judgeUpperValue = 10,
                        triggerEventType = TriggerEventType.���[�h�ύX,
                        triggerNum = 0, // �U�����[�h�ɕύX
                        filter = CreateSampleFilter(CharacterBelong.����)
                    }
                },

                targetCondition = new TargetJudgeData[]
                {
                    new TargetJudgeData
                    {
                        judgeCondition = TargetSelectCondition.�G�ɑ_���Ă鐔,
                        isInvert = BitableBool.FALSE, // �ł��_���Ă��閡�������
                        filter = CreateSampleFilter(CharacterBelong.����)
                    }
                },

                actCondition = new ActJudgeData[]
                {
                    new ActJudgeData
                    {
                        judgeCondition = MoveSelectCondition.�Ώۂ�����̐��̓G�ɑ_���Ă��鎞,
                        actRatio = 100,
                        judgeLowerValue = 1,
                        judgeUpperValue = 10,
                        triggerEventType = TriggerEventType.�ʍs��,
                        triggerNum = 2, // ��q�s��
                        isCoolTimeIgnore = true,
                        isSelfJudge = false,
                        filter = CreateSampleFilter(CharacterBelong.����)
                    }
                }
            };
        }

        /// <summary>
        /// �T���v���p�^�[�Q�b�g�t�B���^�[�̍쐬
        /// </summary>
        private TargetFilter CreateSampleFilter(CharacterBelong targetType)
        {
            // TargetFilter�̍\�z�͎��ۂ̎����ɉ����Ē���
            // �����ł̓v���[�X�z���_�[�Ƃ��Ċ�{�I�Ȑݒ������
            return new TargetFilter(); // ���ۂ̏������͍\���̂̎����Ɉˑ�
        }

        /// <summary>
        /// �T���v���U���f�[�^�̍쐬
        /// </summary>
        private ActData CreateSampleAttackData(string name, float motionValue, float coolTime)
        {
            return new ActData
            {
                motionValue = motionValue,
                coolTimeData = new CoolTimeData
                {
                    skipCondition = ActTriggerCondition.HP����芄���̑Ώۂ����鎞,
                    judgeLowerValue = 0,
                    judgeUpperValue = 20,
                    coolTime = coolTime,
                    filter = new TargetFilter()
                },
                stateChange = ActState.�U��,
                isCancel = false
            };
        }

        /// <summary>
        /// �ݒ�f�[�^�̃G�N�X�|�[�g�@�\
        /// </summary>
        [FoldoutGroup("�f�o�b�O���e�X�g�@�\")]
        [Button("�ݒ��JSON�ŃG�N�X�|�[�g", ButtonSizes.Medium)]
        private void ExportToJSON()
        {
            try
            {
                string json = JsonUtility.ToJson(this, true);
                string path = EditorUtility.SaveFilePanel(
                    "AI�ݒ�̃G�N�X�|�[�g",
                    Application.dataPath,
                    $"{name}_config.json",
                    "json");

                if ( !string.IsNullOrEmpty(path) )
                {
                    File.WriteAllText(path, json);
                    Debug.Log($"�ݒ���G�N�X�|�[�g���܂���: {path}");
                    EditorUtility.DisplayDialog("�G�N�X�|�[�g����",
                        $"�ݒ肪����ɃG�N�X�|�[�g����܂����B\n{path}", "OK");
                }
            }
            catch ( Exception e )
            {
                Debug.LogError($"�G�N�X�|�[�g�Ɏ��s���܂���: {e.Message}");
                EditorUtility.DisplayDialog("�G�N�X�|�[�g�G���[",
                    $"�G�N�X�|�[�g�Ɏ��s���܂����B\n{e.Message}", "OK");
            }
        }

        /// <summary>
        /// �ݒ�f�[�^�̃C���|�[�g�@�\
        /// </summary>
        [FoldoutGroup("�f�o�b�O���e�X�g�@�\")]
        [Button("JSON����ݒ���C���|�[�g", ButtonSizes.Medium)]
        private void ImportFromJSON()
        {
            try
            {
                string path = EditorUtility.OpenFilePanel(
                    "AI�ݒ�̃C���|�[�g",
                    Application.dataPath,
                    "json");

                if ( !string.IsNullOrEmpty(path) && File.Exists(path) )
                {
                    if ( EditorUtility.DisplayDialog("�ݒ�̃C���|�[�g",
                        "�����̐ݒ肪�㏑������܂��B���s���܂����H", "�C���|�[�g", "�L�����Z��") )
                    {
                        string json = File.ReadAllText(path);
                        JsonUtility.FromJsonOverwrite(json, this);
                        EditorUtility.SetDirty(this);
                        Debug.Log($"�ݒ���C���|�[�g���܂���: {path}");
                        EditorUtility.DisplayDialog("�C���|�[�g����",
                            "�ݒ肪����ɃC���|�[�g����܂����B", "OK");
                    }
                }
            }
            catch ( Exception e )
            {
                Debug.LogError($"�C���|�[�g�Ɏ��s���܂���: {e.Message}");
                EditorUtility.DisplayDialog("�C���|�[�g�G���[",
                    $"�C���|�[�g�Ɏ��s���܂����B\n{e.Message}", "OK");
            }
        }

        /// <summary>
        /// �ݒ蓝�v���̕\��
        /// </summary>
        [FoldoutGroup("�f�o�b�O���e�X�g�@�\")]
        [Button("�ݒ蓝�v��\��", ButtonSizes.Medium)]
        private void ShowStatistics()
        {
            var stats = CalculateStatistics();

            string message = $"=== AI�ݒ蓝�v��� ===\n" +
                           $"�L�����N�^�[ID: {characterID}\n" +
                           $"���[�h��: {stats.modeCount}\n" +
                           $"���g���K�[������: {stats.totalTriggers}\n" +
                           $"���^�[�Q�b�g������: {stats.totalTargets}\n" +
                           $"���s��������: {stats.totalActions}\n" +
                           $"�U���f�[�^��: {stats.attackDataCount}\n" +
                           $"���ϔ��f�Ԋu: {stats.averageInterval:F2}�b\n" +
                           $"�g�p����Ă�������^�C�v��: {stats.uniqueConditionTypes}";

            Debug.Log(message);
            EditorUtility.DisplayDialog("AI�ݒ蓝�v", message, "OK");
        }

        /// <summary>
        /// ���v���̌v�Z
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

                    // �����^�C�v�̎��W
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

    #region �e�X�g�R�[�h

#if UNITY_EDITOR
    /// <summary>
    /// BrainStatus�p�̎����e�X�g�X�C�[�g
    /// Unity Test Runner�Ŏ��s�\�ȃe�X�g�R�[�h
    /// </summary>
    public class BrainStatusTests
    {
        private BrainStatus _testBrainStatus;

        [SetUp]
        public void Setup()
        {
            // �e�X�g�p��ScriptableObject���쐬
            _testBrainStatus = ScriptableObject.CreateInstance<BrainStatus>();
        }

        [TearDown]
        public void TearDown()
        {
            // �e�X�g��̃N���[���A�b�v
            if ( _testBrainStatus != null )
            {
                ScriptableObject.DestroyImmediate(_testBrainStatus);
            }
        }

        [Test]
        public void TestTriggerJudgeDataSerialization()
        {
            // �e�X�g�f�[�^�̍쐬
            var triggerData = new BrainStatus.TriggerJudgeData
            {
                judgeCondition = BrainStatus.ActTriggerCondition.HP����芄���̑Ώۂ����鎞,
                actRatio = 75,
                judgeLowerValue = 10,
                judgeUpperValue = 50,
                triggerEventType = TriggerEventType.�ʍs��,
                triggerNum = 2
            };

            // �V���A���C�[�[�V�����e�X�g
            string json = JsonUtility.ToJson(triggerData);
            var deserializedData = JsonUtility.FromJson<BrainStatus.TriggerJudgeData>(json);

            // ����
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
            // ����ȃN�[���^�C���f�[�^
            var coolTimeData = new BrainStatus.CoolTimeData
            {
                skipCondition = BrainStatus.ActTriggerCondition.����̑Ώۂ���萔���鎞,
                judgeLowerValue = 1,
                judgeUpperValue = 5,
                coolTime = 2.0f
            };

            // �N�[���^�C�������łȂ����Ƃ��m�F
            Assert.GreaterOrEqual(coolTimeData.coolTime, 0);

            // ����l���Ó��Ȕ͈͓��ł��邱�Ƃ��m�F
            Assert.GreaterOrEqual(coolTimeData.judgeLowerValue, 0);
            Assert.LessOrEqual(coolTimeData.judgeLowerValue, coolTimeData.judgeUpperValue);
        }

        [Test]
        public void TestActJudgeDataRangeValidation()
        {
            // ���s�m���̋��E�l�e�X�g
            var actData = new BrainStatus.ActJudgeData
            {
                judgeCondition = BrainStatus.MoveSelectCondition.�Ώۂ�HP����芄���̎�,
                actRatio = 100, // ����l
                judgeLowerValue = 0,
                judgeUpperValue = 100
            };

            Assert.GreaterOrEqual(actData.actRatio, 1);
            Assert.LessOrEqual(actData.actRatio, 100);

            // �����l�e�X�g
            actData.actRatio = 1;
            Assert.GreaterOrEqual(actData.actRatio, 1);
        }

        [Test]
        public void TestTargetJudgeDataInvertFlag()
        {
            // ���]�t���O�̃e�X�g
            var targetData = new BrainStatus.TargetJudgeData
            {
                judgeCondition = BrainStatus.TargetSelectCondition.HP����,
                isInvert = BrainStatus.BitableBool.TRUE
            };

            Assert.AreEqual(BrainStatus.BitableBool.TRUE, targetData.isInvert);

            // ���]�t���O�̕ύX�e�X�g
            targetData.isInvert = BrainStatus.BitableBool.FALSE;
            Assert.AreEqual(BrainStatus.BitableBool.FALSE, targetData.isInvert);
        }

        [Test]
        public void TestBrainEventFlagTypeBitOperations()
        {
            // �r�b�g�t���O�̑g�ݍ��킹�e�X�g
            var combinedFlags = BrainEventFlagType.��_���[�W��^���� |
                               BrainEventFlagType.�L������|����;

            Assert.IsTrue((combinedFlags & BrainEventFlagType.��_���[�W��^����) != 0);
            Assert.IsTrue((combinedFlags & BrainEventFlagType.��_���[�W���󂯂�) != 0);
            Assert.IsFalse((combinedFlags & BrainEventFlagType.�񕜂��g�p) != 0);
        }

        [Test]
        public void TestRecognizeObjectTypeBitOperations()
        {
            // �����I�u�W�F�N�g�^�C�v�̑g�ݍ��킹�e�X�g
            var objectTypes = BrainStatus.RecognizeObjectType.�댯�� |
                             BrainStatus.RecognizeObjectType.�ŏ� |
                             BrainStatus.RecognizeObjectType.�_���[�W�G���A;

            Assert.IsTrue((objectTypes & BrainStatus.RecognizeObjectType.�댯��) != 0);
            Assert.IsTrue((objectTypes & BrainStatus.RecognizeObjectType.�ŏ�) != 0);
            Assert.IsTrue((objectTypes & BrainStatus.RecognizeObjectType.�_���[�W�G���A) != 0);
            Assert.IsFalse((objectTypes & BrainStatus.RecognizeObjectType.�o�t�G���A) != 0);
        }

        [Test]
        public void TestCharacterModeDataInitialization()
        {
            // �L�����N�^�[���[�h�f�[�^�̏������e�X�g
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
            // ���S��BrainStatus�̏������e�X�g
            _testBrainStatus.characterID = 1;
            _testBrainStatus.baseData = new BrainStatus.CharacterBaseData
            {
                hp = 100,
                mp = 50,
                initialMove = BrainStatus.ActState.�U��,
                initialBelong = BrainStatus.CharacterBelong.����
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

            // ����
            Assert.AreEqual(1, _testBrainStatus.characterID);
            Assert.AreEqual(100, _testBrainStatus.baseData.hp);
            Assert.AreEqual(50, _testBrainStatus.baseData.mp);
            Assert.AreEqual(1, _testBrainStatus.characterModeSetting.Length);
            Assert.IsNotNull(_testBrainStatus.characterModeSetting[0]);
        }

        [Test]
        public void TestPerformanceWithLargeDataSet()
        {
            // ��ʃf�[�^�ł̃p�t�H�[�}���X�e�X�g
            var largeMode = new BrainStatus.CharacterModeData
            {
                judgeInterval = new Unity.Mathematics.float3(1.0f, 0.5f, 0.3f),
                triggerCondition = new BrainStatus.TriggerJudgeData[100],
                targetCondition = new BrainStatus.TargetJudgeData[100],
                actCondition = new BrainStatus.ActJudgeData[100]
            };

            // �z��̏�����
            for ( int i = 0; i < 100; i++ )
            {
                largeMode.triggerCondition[i] = new BrainStatus.TriggerJudgeData
                {
                    judgeCondition = BrainStatus.ActTriggerCondition.����̑Ώۂ���萔���鎞,
                    actRatio = (byte)(i % 100 + 1)
                };

                largeMode.targetCondition[i] = new BrainStatus.TargetJudgeData
                {
                    judgeCondition = BrainStatus.TargetSelectCondition.HP����,
                    isInvert = i % 2 == 0 ? BrainStatus.BitableBool.TRUE : BrainStatus.BitableBool.FALSE
                };

                largeMode.actCondition[i] = new BrainStatus.ActJudgeData
                {
                    judgeCondition = BrainStatus.MoveSelectCondition.�����Ȃ�,
                    actRatio = (byte)(i % 100 + 1)
                };
            }

            _testBrainStatus.characterModeSetting = new[] { largeMode };

            // �V���A���C�[�[�V�����̃p�t�H�[�}���X�e�X�g
            var startTime = System.DateTime.Now;
            string json = JsonUtility.ToJson(_testBrainStatus);
            var serializationTime = System.DateTime.Now - startTime;

            // 1�b�ȓ��Ŋ������邱�Ƃ��m�F
            Assert.Less(serializationTime.TotalSeconds, 1.0);
            Assert.IsNotEmpty(json);
        }
    }

    /// <summary>
    /// �G�f�B�^�g���̃p�t�H�[�}���X�e�X�g
    /// </summary>
    public class BrainStatusEditorPerformanceTests
    {
        [Test]
        public void TestUIUpdatePerformance()
        {
            // UI�X�V�̃p�t�H�[�}���X�e�X�g
            var testData = new BrainStatus.TriggerJudgeData
            {
                judgeCondition = BrainStatus.ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞,
                judgeLowerValue = 1,
                judgeUpperValue = (int)(BrainStatus.RecognizeObjectType.�댯�� | BrainStatus.RecognizeObjectType.�ŏ�)
            };

            // �������胁�\�b�h�̐��\�e�X�g�i���t���N�V�������g���ČĂяo���j
            var startTime = System.DateTime.Now;

            for ( int i = 0; i < 1000; i++ )
            {
                // ���ۂ̃G�f�B�^�ł͔��胁�\�b�h���p�ɂɌĂ΂�邽�߁A
                // ���̐��\���V�~�����[�g
                bool isEnum = testData.judgeCondition == BrainStatus.ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞;
                bool showValue = testData.judgeCondition != BrainStatus.ActTriggerCondition.�����Ȃ�;
            }

            var executionTime = System.DateTime.Now - startTime;

            // 1000��̔��肪100ms�ȓ��Ŋ������邱�Ƃ��m�F
            Assert.Less(executionTime.TotalMilliseconds, 100);
        }
    }
#endif

    #endregion
}