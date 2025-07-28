#if UNITY_EDITOR
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static CharacterController.AIManager;
using static CharacterController.StatusData.BrainStatus.TriggerJudgeData;

namespace CharacterController.StatusData
{
    /// <summary>
    /// BrainStatus�̃G�f�B�^��p�g���N���X
    /// ���̃N���X��Unity Editor�ł̂ݓ��삵�A���s���ɂ͊��S�ɏ��O�����
    /// AI�L�����N�^�[�̍s������f�[�^�����o�I�ɕҏW���邽�߂�UI�g���@�\���
    /// </summary>
    public partial class BrainStatus
    {
        #region �G�f�B�^��p�v���p�e�B - TriggerJudgeData

        /// <summary>
        /// AI�̍s���g���K�[����f�[�^�\���̂̃G�f�B�^�g��
        /// �����ɉ����ĈقȂ�^�̃f�[�^��K�؂�UI�ŕ\������
        /// </summary>
        public partial struct TriggerJudgeData
        {
            /// <summary>
            /// �����l��BitableBool�i�_���l�j�Ƃ��Ĉ����G�f�B�^�v���p�e�B
            /// OR����/AND����̑I���Ɏg�p�����
            /// ShowIf�A�g���r���[�g�ɂ��A����������ł̂ݕ\��
            /// </summary>
            [ShowInInspector]
            [ShowIf("@IsLowerValueBittableBool()")]  // �������胁�\�b�h�ŕ\������
            [LabelText("@GetLowerValueLabel()")]     // ���I���x������
            private BitableBool LowerValueAsBitableBool
            {
                get => (BitableBool)judgeLowerValue;  // int�l��BitableBool�ɃL���X�g
                set => judgeLowerValue = (int)value;  // BitableBool��int�l�ɕϊ����ĕۑ�
            }

            /// <summary>
            /// �����l��CharacterBelong�i�L�����N�^�[�����j�Ƃ��Ĉ����G�f�B�^�v���p�e�B
            /// ����w�c�̔�������Ŏg�p�����
            /// </summary>
            [ShowInInspector]
            [ShowIf("@IsLowerValueCharacterBelong()")]
            [LabelText("@GetLowerValueLabel()")]
            private CharacterBelong LowerValueAsCharacterBelong
            {
                get => (CharacterBelong)judgeLowerValue;
                set => judgeLowerValue = (int)value;
            }

            /// <summary>
            /// ����l��RecognizeObjectType�i�F���I�u�W�F�N�g��ʁj�Ƃ��Ĉ����G�f�B�^�v���p�e�B
            /// ���͂̃I�u�W�F�N�g�E�n�`����Ŏg�p�����
            /// EnumToggleButtons�Ńr�b�g�t���O�����o�I�ɑI���\
            /// </summary>
            [ShowInInspector]
            [ShowIf("@IsUpperValueRecognizeObject()")]
            [LabelText("@GetUpperValueLabel()")]
            [EnumToggleButtons]  // �����I���\�ȃg�O���{�^��UI
            private RecognizeObjectType UpperValueAsRecognizeObject
            {
                get => (RecognizeObjectType)judgeUpperValue;
                set => judgeUpperValue = (int)value;
            }

            /// <summary>
            /// ����l��BrainEventFlagType�i�]�C�x���g�t���O��ʁj�Ƃ��Ĉ����G�f�B�^�v���p�e�B
            /// ����C�x���g��������Ŏg�p�����
            /// </summary>
            [ShowInInspector]
            [ShowIf("@IsUpperValueBrainEvent()")]
            [LabelText("@GetUpperValueLabel()")]
            [EnumToggleButtons]
            private BrainEventFlagType UpperValueAsBrainEvent
            {
                get => (BrainEventFlagType)judgeUpperValue;
                set => judgeUpperValue = (int)value;
            }

            /// <summary>
            /// �ʍs���I��p�̃h���b�v�_�E���v���p�e�B
            /// attackData�z�񂩂���s����s�����C���f�b�N�X�őI��
            /// ValueDropdown�œ��I�Ƀ��X�g�𐶐����A���o�I�ɑI���\
            /// </summary>
            [ShowInInspector]
            [ShowIf("@IsIndividualActionMode()")]  // �ʍs�����[�h���̂ݕ\��
            [LabelText("���s����s��")]
            [ValueDropdown("@GetActionDropdownList()")]  // ���I�h���b�v�_�E�����X�g����
            [InfoBox("�U���f�[�^�z�񂩂�s����I�����Ă��������B�z�񂪋�̏ꍇ�͐�ɍU���f�[�^��ݒ肵�Ă��������B",
                     InfoMessageType.Info, "@IsActionDataEmpty()")]  // �x�����b�Z�[�W�\������
            private byte TriggerNumAsActionIndex
            {
                get => triggerNum;
                set => triggerNum = value;
            }

            #region �G�f�B�^��p���\�b�h

            /// <summary>
            /// ��������Ɋ�Â��Đ������𐶐����郁�\�b�h
            /// Inspector�ł̗����������邽�߂̏ڍא������
            /// </summary>
            /// <returns>�����ɑΉ�����������</returns>
            private string GetConditionDescription()
            {
                switch ( judgeCondition )
                {
                    case ActTriggerCondition.����̑Ώۂ���萔���鎞:
                        return "�t�B���^�[�ɊY������Ώۂ��w��͈͂̐��������݂��鎞�ɏ�������";
                    case ActTriggerCondition.HP����芄���̑Ώۂ����鎞:
                        return "HP���w��͈͂̊����i0-100%�j�̑Ώۂ����݂��鎞�ɏ�������";
                    case ActTriggerCondition.MP����芄���̑Ώۂ����鎞:
                        return "MP���w��͈͂̊����i0-100%�j�̑Ώۂ����݂��鎞�ɏ�������";
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȏ㖧�W���Ă��鎞:
                        return "�Ώۂ̎��͂Ɏw��w�c���w��l���ȏ㖧�W���Ă��鎞�ɏ�������";
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȉ��������Ȃ���:
                        return "�Ώۂ̎��͂Ɏw��w�c���w��l���ȉ��������Ȃ����ɏ�������";
                    case ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:
                        return "�w�肵���I�u�W�F�N�g�^�C�v�����͂ɑ��݂��鎞�ɏ�������";
                    case ActTriggerCondition.�Ώۂ���萔�̓G�ɑ_���Ă��鎞:
                        return "�w��͈͂̐��̓G����_���Ă��鎞�ɏ�������";
                    case ActTriggerCondition.�Ώۂ̃L�����̈�苗���ȓ��ɔ�ѓ�����鎞:
                        return "��ѓ���w��͈͂̋������ɑ��݂��鎞�ɏ�������";
                    case ActTriggerCondition.����̃C�x���g������������:
                        return "�w�肵���C�x���g�������������ɏ�������";
                    default:
                        return "�����Ȃ�";
                }
            }

            /// <summary>
            /// �����l�t�B�[���h�̓��I���x���������\�b�h
            /// �I�����ꂽ�����ɉ����ēK�؂ȃ��x���e�L�X�g��Ԃ�
            /// </summary>
            /// <returns>�����ɉ��������x��������</returns>
            private string GetLowerValueLabel()
            {
                switch ( judgeCondition )
                {
                    // OR/AND�����I���������
                    case ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:
                    case ActTriggerCondition.����̃C�x���g������������:
                        return "������@�iFALSE:OR����, TRUE:AND����j";
                    // �w�c��I���������
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȏ㖧�W���Ă��鎞:
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȉ��������Ȃ���:
                        return "�Ώېw�c";
                    // ���l�͈͂̉�����ݒ肷�����
                    default:
                        return "�����l";
                }
            }

            /// <summary>
            /// ����l�t�B�[���h�̓��I���x���������\�b�h
            /// �I�����ꂽ�����ɉ����ēK�؂ȃ��x���e�L�X�g��Ԃ�
            /// </summary>
            /// <returns>�����ɉ��������x��������</returns>
            private string GetUpperValueLabel()
            {
                switch ( judgeCondition )
                {
                    // �I�u�W�F�N�g�^�C�v�𕡐��I���������
                    case ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:
                        return "�I�u�W�F�N�g�^�C�v�i�����I���j";
                    // �C�x���g�^�C�v�𕡐��I���������
                    case ActTriggerCondition.����̃C�x���g������������:
                        return "�C�x���g�^�C�v�i�����I���j";
                    // �l����ݒ肷�����
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȏ㖧�W���Ă��鎞:
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȉ��������Ȃ���:
                        return "�K�v�l��";
                    // ���l�͈͂̏����ݒ肷�����
                    default:
                        return "����l";
                }
            }

            // UI�\���������胁�\�b�h�Q
            private bool ShowLowerValue() => judgeCondition != ActTriggerCondition.�����Ȃ�;
            private bool ShowUpperValue() => judgeCondition != ActTriggerCondition.�����Ȃ�;
            private bool IsLowerValueEnum() => IsLowerValueBittableBool() || IsLowerValueCharacterBelong();
            private bool IsUpperValueEnum() => IsUpperValueRecognizeObject() || IsUpperValueBrainEvent();

            /// <summary>
            /// �����l��BitableBool�Ƃ��Ĉ����ׂ��������𔻒�
            /// </summary>
            private bool IsLowerValueBittableBool()
            {
                return judgeCondition == ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞 ||
                       judgeCondition == ActTriggerCondition.����̃C�x���g������������;
            }

            /// <summary>
            /// �����l��CharacterBelong�Ƃ��Ĉ����ׂ��������𔻒�
            /// </summary>
            private bool IsLowerValueCharacterBelong()
            {
                return judgeCondition == ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȏ㖧�W���Ă��鎞 ||
                       judgeCondition == ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȉ��������Ȃ���;
            }

            /// <summary>
            /// ����l��RecognizeObjectType�Ƃ��Ĉ����ׂ��������𔻒�
            /// </summary>
            private bool IsUpperValueRecognizeObject()
            {
                return judgeCondition == ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞;
            }

            /// <summary>
            /// ����l��BrainEventFlagType�Ƃ��Ĉ����ׂ��������𔻒�
            /// </summary>
            private bool IsUpperValueBrainEvent()
            {
                return judgeCondition == ActTriggerCondition.����̃C�x���g������������;
            }

            // �s���I���h���b�v�_�E���֘A�̃��\�b�h�Q

            /// <summary>
            /// �ʍs�����[�h���ǂ����𔻒�
            /// �ʍs�����[�h���͍U���f�[�^�z�񂩂�̍s���I��UI��\��
            /// </summary>
            private bool IsIndividualActionMode() => triggerEventType == TriggerEventType.�ʍs��;

            /// <summary>
            /// �ʏ�̃g���K�[�ԍ��t�B�[���h��\�����邩�𔻒�
            /// �ʍs�����[�h�ȊO�ł͒ʏ�̐��l���̓t�B�[���h��\��
            /// </summary>
            private bool ShowNormalTriggerNum() => !IsIndividualActionMode();

            /// <summary>
            /// �U���f�[�^�z�񂪋󂩂ǂ����𔻒�
            /// ��̏ꍇ�͌x�����b�Z�[�W��\��
            /// </summary>
            private bool IsActionDataEmpty() => GetCurrentBrainStatusActionCount() == 0;

            /// <summary>
            /// �s���I��p�̃h���b�v�_�E�����X�g�𓮓I����
            /// BrainStatus��attackData�z�񂩂�s�����ƃC���f�b�N�X�̃y�A���쐬
            /// </summary>
            /// <returns>ValueDropdown�Ŏg�p���郊�X�g</returns>
            private ValueDropdownList<byte> GetActionDropdownList()
            {
                var list = new ValueDropdownList<byte>();
                var brainStatus = GetCurrentEditingBrainStatus();

                // BrainStatus�ƍU���f�[�^�����݂���ꍇ
                if ( brainStatus != null && brainStatus.attackData != null )
                {
                    // �e�U���f�[�^�ɑ΂��ăh���b�v�_�E�����ڂ��쐬
                    for ( int i = 0; i < brainStatus.attackData.Length; i++ )
                    {
                        var actionData = brainStatus.attackData[i];
                        // �A�N�V���������ݒ肳��Ă��Ȃ��ꍇ�̓f�t�H���g���𐶐�
                        string displayName = string.IsNullOrEmpty(actionData.actionName)
                            ? GenerateDefaultActionName(actionData, i)
                            : actionData.actionName;
                        list.Add(displayName, (byte)i);
                    }
                }

                // �f�[�^�����݂��Ȃ��ꍇ�̃t�H�[���o�b�N
                if ( list.Count == 0 )
                {
                    list.Add("�s���f�[�^�Ȃ�", 0);
                }

                return list;
            }

            /// <summary>
            /// ���ݕҏW����BrainStatus�C���X�^���X���擾
            /// �����̕��@��BrainStatus�̎Q�Ƃ����s���A�ł��K�؂Ȃ��̂�Ԃ�
            /// </summary>
            /// <returns>���ݕҏW����BrainStatus�C���X�^���X�A�܂��� null</returns>
            private BrainStatus GetCurrentEditingBrainStatus()
            {
                // ���@1: Selection.activeObject���璼�ڎ擾
                if ( Selection.activeObject is BrainStatus directBrainStatus )
                {
                    return directBrainStatus;
                }

                // ���@2: Selection.objects���猟���擾
                var selectedObjects = Selection.objects;
                foreach ( var obj in selectedObjects )
                {
                    if ( obj is BrainStatus brainStatus )
                    {
                        return brainStatus;
                    }
                }

                // ���@3: �ÓI�L���b�V������t�H�[���o�b�N�擾
                // Inspector�؂�ւ����ɎQ�Ƃ�������ꍇ�̕ی�
                return BrainStatusEditorCache.CurrentEditingBrainStatus;
            }

            /// <summary>
            /// ���݂�BrainStatus�����U���f�[�^�̐����擾
            /// �h���b�v�_�E�����X�g�̐�����o���f�[�V�����Ŏg�p
            /// </summary>
            /// <returns>�U���f�[�^�z��̒���</returns>
            private int GetCurrentBrainStatusActionCount()
            {
                var brainStatus = GetCurrentEditingBrainStatus();
                return brainStatus.attackData.Length;
            }

            /// <summary>
            /// �U���f�[�^����f�t�H���g�̍s�����𐶐�
            /// actionName���ݒ肳��Ă��Ȃ��ꍇ�̑�֕\�������쐬
            /// </summary>
            /// <param name="actionData">�U���f�[�^</param>
            /// <param name="index">�z����̃C���f�b�N�X</param>
            /// <returns>�������ꂽ�f�t�H���g�s����</returns>
            private string GenerateDefaultActionName(ActData actionData, int index)
            {
                // �s����ԂɊ�Â��ăx�[�X��������
                string baseName = actionData.stateChange switch
                {
                    ActState.�U�� => "�U��",
                    ActState.�h�� => "�h��",
                    ActState.�ړ� => "�ړ�",
                    ActState.���� => "����",
                    ActState.�x�� => "�x��",
                    ActState.�� => "��",
                    _ => "�s��"  // �f�t�H���g
                };

                // motionValue�Ɋ�Â��ċ��x��t��
                if ( actionData.motionValue > 0 )
                {
                    if ( actionData.motionValue >= 2.0f )
                        return $"{baseName}(��) [{index}]";
                    else if ( actionData.motionValue >= 1.5f )
                        return $"{baseName}(��) [{index}]";
                    else
                        return $"{baseName}(��) [{index}]";
                }

                return $"{baseName} [{index}]";
            }

            #endregion
        }

        #endregion

        #region �G�f�B�^��p�v���p�e�B - CoolTimeData

        /// <summary>
        /// �N�[���^�C���f�[�^�\���̂̃G�f�B�^�g��
        /// �s���̃N�[���^�C���X�L�b�v������ݒ肷��UI
        /// TriggerJudgeData�Ɨގ��̍\�������A�N�[���^�C�����L�̋@�\������
        /// </summary>
        public partial struct CoolTimeData
        {
            // �ȉ��̃v���p�e�B��TriggerJudgeData�Ɠ��l�̋@�\
            // �N�[���^�C���X�L�b�v�����ł̎g�p�ɓ���

            [ShowInInspector]
            [ShowIf("@IsLowerValueBittableBool()")]
            [LabelText("@GetLowerValueLabel()")]
            private BitableBool LowerValueAsBitableBool
            {
                get => (BitableBool)judgeLowerValue;
                set => judgeLowerValue = (int)value;
            }

            [ShowInInspector]
            [ShowIf("@IsLowerValueCharacterBelong()")]
            [LabelText("@GetLowerValueLabel()")]
            private CharacterBelong LowerValueAsCharacterBelong
            {
                get => (CharacterBelong)judgeLowerValue;
                set => judgeLowerValue = (int)value;
            }

            [ShowInInspector]
            [ShowIf("@IsUpperValueRecognizeObject()")]
            [LabelText("@GetUpperValueLabel()")]
            [EnumToggleButtons]
            private RecognizeObjectType UpperValueAsRecognizeObject
            {
                get => (RecognizeObjectType)judgeUpperValue;
                set => judgeUpperValue = (int)value;
            }

            [ShowInInspector]
            [ShowIf("@IsUpperValueBrainEvent()")]
            [LabelText("@GetUpperValueLabel()")]
            [EnumToggleButtons]
            private BrainEventFlagType UpperValueAsBrainEvent
            {
                get => (BrainEventFlagType)judgeUpperValue;
                set => judgeUpperValue = (int)value;
            }

            #region �G�f�B�^��p���\�b�h�iCoolTimeData�p�j

            /// <summary>
            /// �X�L�b�v�����̐������𐶐�
            /// �N�[���^�C�����X�L�b�v��������̏ڍׂ�\��
            /// </summary>
            private string GetSkipConditionDescription()
            {
                switch ( skipCondition )
                {
                    case ActTriggerCondition.����̑Ώۂ���萔���鎞:
                        return "�t�B���^�[�ɊY������Ώۂ��w��͈͂̐��������݂���ꍇ�ɃN�[���^�C�����X�L�b�v";
                    case ActTriggerCondition.HP����芄���̑Ώۂ����鎞:
                        return "HP���w��͈͂̊����̑Ώۂ����݂���ꍇ�ɃN�[���^�C�����X�L�b�v";
                    case ActTriggerCondition.MP����芄���̑Ώۂ����鎞:
                        return "MP���w��͈͂̊����̑Ώۂ����݂���ꍇ�ɃN�[���^�C�����X�L�b�v";
                    default:
                        return "�N�[���^�C���X�L�b�v�����Ȃ�";
                }
            }

            /// <summary>
            /// �����l���x�������iCoolTimeData�p�j
            /// skipCondition�Ɋ�Â��ēK�؂ȃ��x����Ԃ�
            /// </summary>
            private string GetLowerValueLabel()
            {
                switch ( skipCondition )
                {
                    case ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:
                    case ActTriggerCondition.����̃C�x���g������������:
                        return "������@�iFALSE:OR����, TRUE:AND����j";
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȏ㖧�W���Ă��鎞:
                    case ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȉ��������Ȃ���:
                        return "�Ώېw�c";
                    default:
                        return "�����l";
                }
            }

            /// <summary>
            /// ����l���x�������iCoolTimeData�p�j
            /// skipCondition�Ɋ�Â��ēK�؂ȃ��x����Ԃ�
            /// </summary>
            private string GetUpperValueLabel()
            {
                switch ( skipCondition )
                {
                    case ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:
                        return "�I�u�W�F�N�g�^�C�v�i�����I���j";
                    case ActTriggerCondition.����̃C�x���g������������:
                        return "�C�x���g�^�C�v�i�����I���j";
                    default:
                        return "����l";
                }
            }

            // UI�\���������胁�\�b�h�Q�iskipCondition�x�[�X�j
            private bool ShowLowerValue() => skipCondition != ActTriggerCondition.�����Ȃ�;
            private bool ShowUpperValue() => skipCondition != ActTriggerCondition.�����Ȃ�;
            private bool IsLowerValueEnum() => IsLowerValueBittableBool() || IsLowerValueCharacterBelong();
            private bool IsUpperValueEnum() => IsUpperValueRecognizeObject() || IsUpperValueBrainEvent();

            private bool IsLowerValueBittableBool()
            {
                return skipCondition == ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞 ||
                       skipCondition == ActTriggerCondition.����̃C�x���g������������;
            }

            private bool IsLowerValueCharacterBelong()
            {
                return skipCondition == ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȏ㖧�W���Ă��鎞 ||
                       skipCondition == ActTriggerCondition.�Ώۂ̃L�����̎��͂ɓ���w�c�����ȉ��������Ȃ���;
            }

            private bool IsUpperValueRecognizeObject()
            {
                return skipCondition == ActTriggerCondition.���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞;
            }

            private bool IsUpperValueBrainEvent()
            {
                return skipCondition == ActTriggerCondition.����̃C�x���g������������;
            }

            #endregion
        }

        #endregion

        #region �G�f�B�^��p�v���p�e�B - ActJudgeData

        /// <summary>
        /// �s������f�[�^�\���̂̃G�f�B�^�g��
        /// ����̍s�������s���邩�ǂ����̔��������ݒ�
        /// MoveSelectCondition���g�p���Ă��ڍׂȍs��������s��
        /// </summary>
        public partial struct ActJudgeData
        {
            // ��{�I�Ȍ^�ϊ��v���p�e�B�͑��̍\���̂Ɠ��l
            [ShowInInspector]
            [ShowIf("@IsLowerValueBittableBool()")]
            [LabelText("@GetLowerValueLabel()")]
            private BitableBool LowerValueAsBitableBool
            {
                get => (BitableBool)judgeLowerValue;
                set => judgeLowerValue = (int)value;
            }

            [ShowInInspector]
            [ShowIf("@IsLowerValueCharacterBelong()")]
            [LabelText("@GetLowerValueLabel()")]
            private CharacterBelong LowerValueAsCharacterBelong
            {
                get => (CharacterBelong)judgeLowerValue;
                set => judgeLowerValue = (int)value;
            }

            [ShowInInspector]
            [ShowIf("@IsUpperValueRecognizeObject()")]
            [LabelText("@GetUpperValueLabel()")]
            [EnumToggleButtons]
            private RecognizeObjectType UpperValueAsRecognizeObject
            {
                get => (RecognizeObjectType)judgeUpperValue;
                set => judgeUpperValue = (int)value;
            }

            [ShowInInspector]
            [ShowIf("@IsUpperValueBrainEvent()")]
            [LabelText("@GetUpperValueLabel()")]
            [EnumToggleButtons]
            private BrainEventFlagType UpperValueAsBrainEvent
            {
                get => (BrainEventFlagType)judgeUpperValue;
                set => judgeUpperValue = (int)value;
            }

            /// <summary>
            /// �ʍs���I��p�̃h���b�v�_�E���v���p�e�B�iActJudgeData�Łj
            /// TriggerJudgeData�Ɠ��l�̋@�\�����A�s�������p
            /// </summary>
            [ShowInInspector]
            [ShowIf("@IsIndividualActionMode()")]
            [LabelText("���s����s��")]
            [ValueDropdown("@GetActionDropdownList()")]
            [InfoBox("�U���f�[�^�z�񂩂�s����I�����Ă��������B", InfoMessageType.Info, "@IsActionDataEmpty()")]
            private byte TriggerNumAsActionIndex
            {
                get => triggerNum;
                set => triggerNum = value;
            }

            #region �G�f�B�^��p���\�b�h�iActJudgeData�p�j

            /// <summary>
            /// �s����������̐������𐶐�
            /// MoveSelectCondition�Ɋ�Â����������
            /// </summary>
            private string GetConditionDescription()
            {
                switch ( judgeCondition )
                {
                    case MoveSelectCondition.�Ώۂ��t�B���^�[�ɓ��Ă͂܂鎞:
                        return "�t�B���^�[�����ɊY������Ώۂ����݂��鎞�ɍs�����s";
                    case MoveSelectCondition.�Ώۂ�HP����芄���̎�:
                        return "�Ώۂ�HP���w��͈͂̊����̎��ɍs�����s";
                    case MoveSelectCondition.�Ώۂ�MP����芄���̎�:
                        return "�Ώۂ�MP���w��͈͂̊����̎��ɍs�����s";
                    case MoveSelectCondition.�^�[�Q�b�g�������̏ꍇ:
                        return "�^�[�Q�b�g���������g�̏ꍇ�ɍs�����s";
                    default:
                        return "�����Ȃ� - ��ɍs�����s";
                }
            }

            /// <summary>
            /// �����l���x�������iActJudgeData�p�j
            /// MoveSelectCondition�Ɋ�Â��ă��x��������
            /// </summary>
            private string GetLowerValueLabel()
            {
                switch ( judgeCondition )
                {
                    case MoveSelectCondition.�Ώۂ̎��͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:
                    case MoveSelectCondition.����̃C�x���g������������:
                        return "������@�iFALSE:OR����, TRUE:AND����j";
                    case MoveSelectCondition.�Ώۂ̎��͂ɓ���w�c�̃L���������ȏ㖧�W���Ă��鎞:
                    case MoveSelectCondition.�Ώۂ̎��͂ɓ���w�c�̃L���������ȉ��������Ȃ���:
                        return "�Ώېw�c";
                    default:
                        return "�����l";
                }
            }

            /// <summary>
            /// ����l���x�������iActJudgeData�p�j
            /// MoveSelectCondition�Ɋ�Â��ă��x��������
            /// </summary>
            private string GetUpperValueLabel()
            {
                switch ( judgeCondition )
                {
                    case MoveSelectCondition.�Ώۂ̎��͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞:
                        return "�I�u�W�F�N�g�^�C�v�i�����I���j";
                    case MoveSelectCondition.����̃C�x���g������������:
                        return "�C�x���g�^�C�v�i�����I���j";
                    default:
                        return "����l";
                }
            }

            /// <summary>
            /// �����l�t�B�[���h�̕\����������
            /// ����̏����ł͉����l���s�v�Ȃ��ߔ�\���ɂ���
            /// </summary>
            private bool ShowLowerValue()
            {
                return judgeCondition != MoveSelectCondition.�����Ȃ� &&
                       judgeCondition != MoveSelectCondition.�Ώۂ��t�B���^�[�ɓ��Ă͂܂鎞 &&
                       judgeCondition != MoveSelectCondition.�^�[�Q�b�g�������̏ꍇ;
            }

            /// <summary>
            /// ����l�t�B�[���h�̕\����������
            /// ����̏����ł͏���l���s�v�Ȃ��ߔ�\���ɂ���
            /// </summary>
            private bool ShowUpperValue()
            {
                return judgeCondition != MoveSelectCondition.�����Ȃ� &&
                       judgeCondition != MoveSelectCondition.�Ώۂ��t�B���^�[�ɓ��Ă͂܂鎞 &&
                       judgeCondition != MoveSelectCondition.�^�[�Q�b�g�������̏ꍇ;
            }

            // UI�\���������胁�\�b�h�Q
            private bool IsLowerValueEnum() => IsLowerValueBittableBool() || IsLowerValueCharacterBelong();
            private bool IsUpperValueEnum() => IsUpperValueRecognizeObject() || IsUpperValueBrainEvent();

            private bool IsLowerValueBittableBool()
            {
                return judgeCondition == MoveSelectCondition.�Ώۂ̎��͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞 ||
                       judgeCondition == MoveSelectCondition.����̃C�x���g������������;
            }

            private bool IsLowerValueCharacterBelong()
            {
                return judgeCondition == MoveSelectCondition.�Ώۂ̎��͂ɓ���w�c�̃L���������ȏ㖧�W���Ă��鎞 ||
                       judgeCondition == MoveSelectCondition.�Ώۂ̎��͂ɓ���w�c�̃L���������ȉ��������Ȃ���;
            }

            private bool IsUpperValueRecognizeObject()
            {
                return judgeCondition == MoveSelectCondition.�Ώۂ̎��͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞;
            }

            private bool IsUpperValueBrainEvent()
            {
                return judgeCondition == MoveSelectCondition.����̃C�x���g������������;
            }

            // �s���I���h���b�v�_�E���֘A�iActJudgeData�Łj
            // TriggerJudgeData�Ɠ������������A�s������R���e�L�X�g�Ŏg�p
            private bool IsIndividualActionMode() => triggerEventType == TriggerEventType.�ʍs��;
            private bool ShowNormalTriggerNum() => !IsIndividualActionMode();
            private bool IsActionDataEmpty() => GetCurrentBrainStatusActionCount() == 0;

            /// <summary>
            /// �s���I���h���b�v�_�E�����X�g�����iActJudgeData�Łj
            /// TriggerJudgeData�Ɠ����������ė��p
            /// </summary>
            private ValueDropdownList<byte> GetActionDropdownList()
            {
                var list = new ValueDropdownList<byte>();
                var brainStatus = GetCurrentEditingBrainStatus();

                if ( brainStatus != null && brainStatus.attackData != null )
                {
                    for ( int i = 0; i < brainStatus.attackData.Length; i++ )
                    {
                        var actionData = brainStatus.attackData[i];
                        string displayName = string.IsNullOrEmpty(actionData.actionName)
                            ? GenerateDefaultActionName(actionData, i)
                            : actionData.actionName;
                        list.Add(displayName, (byte)i);
                    }
                }

                if ( list.Count == 0 )
                {
                    list.Add("�s���f�[�^�Ȃ�", 0);
                }

                return list;
            }

            /// <summary>
            /// BrainStatus�Q�Ǝ擾�iActJudgeData�Łj
            /// �����̕��@�ŃC���X�^���X�擾�����s
            /// </summary>
            private BrainStatus GetCurrentEditingBrainStatus()
            {
                // ���@1: Selection.activeObject����擾
                if ( Selection.activeObject is BrainStatus directBrainStatus )
                {
                    return directBrainStatus;
                }

                // ���@2: Selection.objects����擾
                var selectedObjects = Selection.objects;
                foreach ( var obj in selectedObjects )
                {
                    if ( obj is BrainStatus brainStatus )
                    {
                        return brainStatus;
                    }
                }

                // ���@3: �ÓI�L���b�V������擾�i�t�H�[���o�b�N�j
                return BrainStatusEditorCache.CurrentEditingBrainStatus;
            }

            /// <summary>
            /// �U���f�[�^���擾�iActJudgeData�Łj
            /// </summary>
            private int GetCurrentBrainStatusActionCount()
            {
                var brainStatus = GetCurrentEditingBrainStatus();
                return brainStatus.attackData.Length;
            }

            /// <summary>
            /// �f�t�H���g�s���������iActJudgeData�Łj
            /// �U���f�[�^����킩��₷���\�����𐶐�
            /// </summary>
            private string GenerateDefaultActionName(ActData actionData, int index)
            {
                string baseName = actionData.stateChange switch
                {
                    ActState.�U�� => "�U��",
                    ActState.�h�� => "�h��",
                    ActState.�ړ� => "�ړ�",
                    ActState.���� => "����",
                    ActState.�x�� => "�x��",
                    ActState.�� => "��",
                    _ => "�s��"
                };

                if ( actionData.motionValue > 0 )
                {
                    if ( actionData.motionValue >= 2.0f )
                        return $"{baseName}(��) [{index}]";
                    else if ( actionData.motionValue >= 1.5f )
                        return $"{baseName}(��) [{index}]";
                    else
                        return $"{baseName}(��) [{index}]";
                }

                return $"{baseName} [{index}]";
            }

            #endregion
        }

        #endregion

        #region �G�f�B�^��p�v���p�e�B - TargetJudgeData

        /// <summary>
        /// �^�[�Q�b�g����f�[�^�\���̂̃G�f�B�^�g��
        /// AI���^�[�Q�b�g��I������ۂ̏�����ݒ�
        /// ���̔���f�[�^���P���ȍ\��������
        /// </summary>
        public partial struct TargetJudgeData
        {
            #region �G�f�B�^��p���\�b�h�iTargetJudgeData�p�j

            /// <summary>
            /// �^�[�Q�b�g�I�������̐������𐶐�
            /// TargetSelectCondition�Ɋ�Â����ڍא������
            /// </summary>
            private string GetConditionDescription()
            {
                string baseDescription = judgeCondition switch
                {
                    TargetSelectCondition.���x => "���x����Ƀ^�[�Q�b�g�I��",
                    TargetSelectCondition.HP���� => "HP��������Ƀ^�[�Q�b�g�I��",
                    TargetSelectCondition.HP => "HP�l����Ƀ^�[�Q�b�g�I��",
                    TargetSelectCondition.���� => "�������g���^�[�Q�b�g�ɐݒ�",
                    TargetSelectCondition.�v���C���[ => "�v���C���[���^�[�Q�b�g�ɐݒ�",
                    TargetSelectCondition.�V�X�^�[���� => "�V�X�^�[������^�[�Q�b�g�ɐݒ�",
                    TargetSelectCondition.�w��Ȃ�_�t�B���^�[�̂� => "�t�B���^�[�����݂̂Ń^�[�Q�b�g�I��",
                    _ => "�^�[�Q�b�g�I������"
                };

                return baseDescription;
            }

            /// <summary>
            /// ���]�����̐������𐶐�
            /// isInvert�t���O�Ɋ�Â��ă^�[�Q�b�g�I���̔��]��������
            /// </summary>
            private string GetInvertDescription()
            {
                if ( isInvert == BitableBool.TRUE )
                {
                    return "���]�����K�p: �ő�l���ŏ��l�A�ō����Œ�ɕύX����܂�";
                }
                else
                {
                    return "�ʏ����: �ő�l��ō��̑Ώۂ�I�����܂�";
                }
            }

            #endregion
        }

        #endregion
    }

    #region �ÓI�L���b�V���N���X

    /// <summary>
    /// BrainStatus�G�f�B�^�p�̐ÓI�L���b�V���N���X
    /// Inspector�؂�ւ�����Selection�ύX����BrainStatus�̎Q�Ƃ�ێ�
    /// �G�f�B�^UI�̈��萫�ƈ�ѐ����m�ۂ��邽�߂̏d�v�ȃw���p�[�N���X
    /// </summary>
    public static class BrainStatusEditorCache
    {
        /// <summary>
        /// ���ݕҏW����BrainStatus�C���X�^���X�̐ÓI�L���b�V��
        /// Inspector�؂�ւ���Selection�ύX�ŎQ�Ƃ������邱�Ƃ�h��
        /// </summary>
        private static BrainStatus _currentEditingBrainStatus;

        /// <summary>
        /// ���ݕҏW����BrainStatus�C���X�^���X�̃v���p�e�B
        /// null�`�F�b�N���܂ވ��S�ȎQ�Ǝ擾���
        /// </summary>
        public static BrainStatus CurrentEditingBrainStatus
        {
            get
            {
                // �L���b�V�����ꂽ�I�u�W�F�N�g���j������Ă���ꍇ��null��Ԃ�
                // Unity���L�̃I�u�W�F�N�g�j�����o���W�b�N
                if ( _currentEditingBrainStatus != null && _currentEditingBrainStatus == null )
                {
                    _currentEditingBrainStatus = null;
                }
                return _currentEditingBrainStatus;
            }
            set => _currentEditingBrainStatus = value;
        }

        /// <summary>
        /// �L���b�V�������S�ɃN���A���郁�\�b�h
        /// �G�f�B�^�I������v���W�F�N�g�؂�ւ����̐��|�p
        /// </summary>
        public static void ClearCache()
        {
            _currentEditingBrainStatus = null;
        }

        /// <summary>
        /// �G�f�B�^���������ɌĂяo����鎩�����������\�b�h
        /// Unity Editor�̋N������Selection�ύX�C�x���g��o�^
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Selection�ύX���ɃL���b�V���������X�V����C�x���g��o�^
            Selection.selectionChanged += OnSelectionChanged;
        }

        /// <summary>
        /// Selection�ύX���̃R�[���o�b�N���\�b�h
        /// �V�����I�����ꂽ�I�u�W�F�N�g��BrainStatus�̏ꍇ�A�L���b�V�����X�V
        /// </summary>
        private static void OnSelectionChanged()
        {
            // �I�����ύX���ꂽ����BrainStatus������΃L���b�V�����X�V
            if ( Selection.activeObject is BrainStatus brainStatus )
            {
                CurrentEditingBrainStatus = brainStatus;
            }
        }
    }

    #endregion

    #region �J�X�^���G�f�B�^

    /// <summary>
    /// BrainStatus�̃J�X�^���G�f�B�^�N���X
    /// OdinEditor���p������Odin Inspector�̋@�\�����p
    /// Unity Inspector�ł�BrainStatus�\�����J�X�^�}�C�Y
    /// </summary>
    [CustomEditor(typeof(BrainStatus))]
    public class BrainStatusEditor : OdinEditor
    {
        /// <summary>
        /// Inspector GUI�̕`����I�[�o�[���C�h
        /// �J�X�^���w�b�_�[�ƃw���v���b�Z�[�W��ǉ�
        /// </summary>
        public override void OnInspectorGUI()
        {
            // �J�X�^���^�C�g���̕\��
            EditorGUILayout.Space();
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,                          // �t�H���g�T�C�Y�g��
                alignment = TextAnchor.MiddleCenter     // ��������
            };
            EditorGUILayout.LabelField("AI���f�f�[�^�ݒ�", titleStyle);
            EditorGUILayout.Space();

            // �W����Odin Inspector�`������s
            // ��L�Œ�`�������ׂẴJ�X�^���v���p�e�B�ƃ��\�b�h���K�p�����
            base.OnInspectorGUI();

            // �w���v�{�b�N�X�̕\��
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "AI���f�f�[�^�̐ݒ��ʂł��B�e�����ɉ����ēK�؂Ȓl��ݒ肵�Ă��������B\n" +
                "�EBitableBool: FALSE=OR����, TRUE=AND����\n" +
                "�E�r�b�g�t���O: �����̏�����g�ݍ��킹�Ďw��\\n" +
                "�E�t�B���^�[: �Ώۂ��i�荞�ނ��߂̏���",
                MessageType.Info);
        }
    }

    #endregion
}
#endif