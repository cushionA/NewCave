using MyTool.Collections;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static CharacterController.AIManager;

namespace CharacterController
{
    /// <summary>
    /// �g�p��ʂɊ�Â��ăf�[�^���\���̂ɐ؂蕪���Ă���B<br/>
    /// 
    /// ���̃N���X�ł̓L�����N�^�[�̐ݒ�̃f�[�^���`���Ă���B<br/>
    /// ������X�e�[�^�X�f�[�^�B<br/>
    /// �ϕ����i���W��HP�j�A�L�����N�^�[�̈ӎv����Ɏg�p���镔���i���f�������j��Job�V�X�e���Ŏg�p����O��Œl�^�݂̂ō\���B<br/>
    /// ���̕�����ScriptableObject�������s�ς̋��L�f�[�^�Ƃ��Ď����Ă���΂������߁A�Q�ƌ^(�G�t�F�N�g�̃f�[�^�Ȃ�)���g���B<br/>
    /// </summary>
    [CreateAssetMenu(fileName = "BrainStatus", menuName = "Scriptable Objects/BrainStatus")]
    public class BrainStatus : SerializedScriptableObject
    {
        #region Enum��`

        /// <summary>
        /// �������s�������肷�邽�߂̏���
        /// �����̎��A�U���E�񕜁E�x���E�����E��q�Ȃ�
        /// �Ώۂ������A�����A�G�̂ǂꂩ�Ƃ����敪�Ɣے�i�ȏオ�ȓ��ɂȂ�����j�t���O�̑g�ݍ��킹�ŕ\�� - > IsInvert�t���O
        /// ���������񂾂Ƃ��A�͎��S��ԂŐ��b�L�������c�����ƂŁA���S�������ɂ����t�B���^�[�ɂ�����悤�ɂ��邩�B
        /// </summary>
        public enum ActJudgeCondition
        {
            �w��̃w�C�g�l�̓G�����鎞 = 1,
            //�Ώۂ���萔�̎� = 2, // �t�B���^�[�����p���邱�ƂŁA�����ł��Ȃ�̐��̒P���ȏ����͂���B��̈ȏ�����Ń^�C�v�t�B���^�[�őΏۂ̃^�C�v�i������
            HP����芄���̑Ώۂ����鎞 = 2,
            MP����芄���̑Ώۂ����鎞 = 3,
            �ݒ苗���ɑΏۂ����鎞 = 4,  //�����n�̏����͕ʂ̂����Ŏ��O�ɃL���b�V�����s���BAI�̐ݒ�͈̔͂����Z���T�[�Œ��ׂ���@���Ƃ�B���f���ɂ��悤�ɂ���H
            ����̑����ōU������Ώۂ����鎞 = 5,
            ����̐��̓G�ɑ_���Ă��鎞 = 6,// �w�c�t�B���^�����O�͗L��
            �����Ȃ� = 0 // �������Ă͂܂�Ȃ��������̕⌇�����B
        }

        /// <summary>
        /// �s�����f������O��
        /// ������MP��HP�̊����Ȃǂ́A�����Ɋւ���O������𔻒f���邽�߂̐ݒ�
        /// </summary>
        public enum SkipJudgeCondition
        {
            ������HP����芄���̎�,
            ������MP����芄���̎�,
            �����Ȃ� // �������Ă͂܂�Ȃ��������̕⌇�����B
        }

        /// <summary>
        /// MoveJudgeCondition�̑Ώۂ̃^�C�v
        /// </summary>
        public enum TargetType
        {
            ���� = 0,
            ���� = 1,
            �G = 2
        }

        /// <summary>
        /// ���f�̌��ʑI�������s���̃^�C�v�B
        /// 
        /// </summary>
        [Flags]
        public enum ActState
        {
            �w��Ȃ� = 0,// �X�e�[�g�t�B���^�[���f�Ŏg���B�����w�肵�Ȃ��B
            �ǐ� = 1 << 0,
            ���� = 1 << 1,
            �U�� = 1 << 2,
            �ҋ@ = 1 << 3,// �U����̃N�[���^�C�����ȂǁB���̏�Ԃœ��삷���𗦂�ݒ肷��H
            �h�� = 1 << 4,// �����o��������ݒ�ł���悤�ɂ���H ���̏�Ŋ�{�K�[�h�����ǁA���肪�����炩���ꂽ�瓮���o���A�I��
            �x�� = 1 << 5,
            �� = 1 << 6,
            �W�� = 1 << 7,// ����̖����̏ꏊ�ɍs���B�W����ɖh��Ɉڍs���郍�W�b�N��g�߂Ό�q�ɂȂ�Ȃ��H
        }

        /// <summary>
        /// �G�ɑ΂���w�C�g�l�̏㏸�A�����̏����B
        /// �����ɓ��Ă͂܂�G�̃w�C�g�l���㏸�����茸�������肷��B
        /// ���邢�͖����̎x���E�񕜁E��q�Ώۂ����߂�
        /// ������ے�t���O�Ƃ̑g�ݍ��킹�Ŏg��
        /// </summary>
        public enum TargetSelectCondition
        {
            ���x,
            HP����,
            HP,
            �G�ɑ_���Ă鐔,//��ԑ_���Ă邩�A�_���ĂȂ���
            ���v�U����,
            ���v�h���,
            �a���U����,//����̑����̍U���͂���ԍ���/�Ⴂ���c
            �h�ˍU����,
            �Ō��U����,
            ���U����,
            ���U����,
            ���U����,
            �ōU����,
            �a���h���,
            �h�˖h���,
            �Ō��h���,
            ���h���,
            ���h���,
            ���h���,
            �Ŗh���,
            ����,
            ����,
            �v���C���[,
            �w��Ȃ�_�w�C�g�l, // ��{�̏����B�Ώۂ̒��ōł��w�C�g����������U������B
            �s�v_��ԕύX// ���[�h�`�F���W����B
        }

        /// <summary>
        /// �L�����N�^�[�̑����B
        /// �����ɓ��Ă͂܂镪�S���Ԃ����ށB
        /// </summary>
        [Flags]
        public enum CharacterFeature
        {
            �v���C���[ = 1 << 0,
            �V�X�^�[���� = 1 << 1,
            NPC = 1 << 2,
            �ʏ�G�l�~�[ = 1 << 3,
            �{�X = 1 << 4,
            ���m = 1 << 5,//���̎G��
            ��s = 1 << 6,//��Ԃ��
            �ˎ� = 1 << 7,//������
            �R�m = 1 << 8,//������
            㩌n = 1 << 9,//�҂��\���Ă���
            ���G = 1 << 10,// ���G
            �U�R = 1 << 11,
            �q�[���[ = 1 << 12,
            �T�|�[�^�[ = 1 << 13,
            ���� = 1 << 14,
            �w���� = 1 << 15,
            �w��Ȃ� = 0//�w��Ȃ�
        }

        /// <summary>
        /// �L�����N�^�[����������w�c
        /// </summary>
        [Flags]
        public enum CharacterSide
        {
            �v���C���[ = 1 << 0,// ����
            ���� = 1 << 1,// ��ʓI�ȓG
            ���̑� = 1 << 2,// ����ȊO
            �w��Ȃ� = 0
        }

        /// <summary>
        /// �����̗񋓌^
        /// ��Ԉُ�͕�����B
        /// </summary>
        [Flags]
        public enum Element
        {
            �a������ = 1 << 0,
            �h�ˑ��� = 1 << 1,
            �Ō����� = 1 << 2,
            ������ = 1 << 3,
            �ő��� = 1 << 4,
            ������ = 1 << 5,
            ������ = 1 << 6,
            �w��Ȃ� = 0
        }

        /// <summary>
        /// �L�����̃��x��
        /// ���̃��x���������Ƒ��̓G�Ɏז�����Ȃ�
        /// </summary>
        public enum CharacterRank
        {
            �U�R,//�G��
            ��͋�,//��{�͂���
            �w����,//�����u
            �{�X//�{�X����
        }

        /// <summary>
        /// ������
        /// </summary>
        [Flags]
        public enum SpecialEffect
        {
            �w�C�g���� = 1 << 1,
            �w�C�g���� = 1 << 2,
            �Ȃ� = 0,
        }

        /// <summary>
        /// bitable�Ȑ^�U�l
        /// Job�V�X�e���A�Ƃ������l�C�e�B�u�R�[�h�� bool �̑������ǂ��Ȃ����ߎ���
        /// </summary>
        public enum BitableBool
        {
            FALSE = 0,
            TRUE = 1
        }

        #endregion Enum��`

        #region �\���̒�`

        /// <summary>
        /// �e�����ɑ΂���U���͂܂��͖h��͂̒l��ێ�����\����
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct ElementalStatus
        {
            /// <summary>
            /// �a�������̒l
            /// </summary>
            [Header("�a������")]
            public int slash;

            /// <summary>
            /// �h�ˑ����̒l
            /// </summary>
            [Header("�h�ˑ���")]
            public int pierce;

            /// <summary>
            /// �Ō������̒l
            /// </summary>
            [Header("�Ō�����")]
            public int strike;

            /// <summary>
            /// �������̒l
            /// </summary>
            [Header("������")]
            public int fire;

            /// <summary>
            /// �������̒l
            /// </summary>
            [Header("������")]
            public int lightning;

            /// <summary>
            /// �������̒l
            /// </summary>
            [Header("������")]
            public int light;

            /// <summary>
            /// �ő����̒l
            /// </summary>
            [Header("�ő���")]
            public int dark;

            /// <summary>
            /// ���v�l��Ԃ��B
            /// </summary>
            /// <returns></returns>
            public int ReturnSum()
            {
                return this.slash + this.pierce + this.strike + this.fire + this.lightning + this.light + this.dark;
            }

        }

        /// <summary>
        /// ���M����f�[�^�A�s�ς̕�
        /// �唼�r�b�g�ł܂Ƃ߂ꂻ��
        /// ���ԋR�m�̓G���邩������Ȃ����^�C�v�͑g�ݍ��킹�\�ɂ���
        /// �������ȍ~�ł́A�X�e�[�^�X�o�t��f�o�t���؂ꂽ���Ɍ��ɖ߂����炢�����Ȃ�
        /// Job�V�X�e���Ŏg�p���Ȃ��̂Ń��������C�A�E�g�͍œK��
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Auto)]
        public struct CharacterBaseData
        {
            /// <summary>
            /// �ő�HP
            /// </summary>
            [Header("HP")]
            public int hp;

            /// <summary>
            /// �ő�MP
            /// </summary>
            [Header("MP")]
            public int mp;

            /// <summary>
            /// �e�����̊�b�U����
            /// </summary>
            [Header("��b�����U����")]
            public ElementalStatus baseAtk;

            /// <summary>
            /// �e�����̊�b�h���
            /// </summary>
            [Header("��b�����h���")]
            public ElementalStatus baseDef;

            /// <summary>
            /// �L�����̏�����ԁB
            /// </summary>
            [Header("�ŏ��ɂǂ�ȍs��������̂��̐ݒ�")]
            public ActState initialMove;

            /// <summary>
            /// �f�t�H���g�̃L�����N�^�[�̏���
            /// </summary>
            public CharacterSide initialBelong;
        }

        /// <summary>
        /// ��ɕς��Ȃ��f�[�^���i�[����\���́B
        /// BaseData�Ƃ̈Ⴂ�́A�������ȍ~�p�ɂɎQ�Ƃ���K�v�����邩�B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct SolidData
        {

            /// <summary>
            /// �O���\���p�̍U���́B
            /// </summary>
            [Header("�\���p�U����")]
            public int displayAtk;

            /// <summary>
            /// �O���\���p�̖h��́B
            /// </summary>
            [Header("�\���p�h���")]
            public int displayDef;

            /// <summary>
            /// �U�������������񋓌^
            /// �r�b�g���Z�Ō���
            /// NPC���������
            /// �Ȃɑ����̍U�������Ă��邩�Ƃ����Ƃ���
            /// </summary>
            [Header("�U������")]
            public Element attackElement;

            /// <summary>
            /// ��_�����������񋓌^
            /// �r�b�g���Z�Ō���
            /// NPC���������
            /// </summary>
            [Header("��_����")]
            public Element weakPoint;

            /// <summary>
            /// �L�����̑����Ƃ�����
            /// �����������B��ނ��
            /// </summary>
            [Header("�L�����N�^�[����")]
            public CharacterFeature feature;

            /// <summary>
            /// �L�����̊K���B<br/>
            /// ���ꂪ��Ȃقǖ����̒��œ����G���^�[�Q�b�g�ɂ��ĂĂ����T�����Ȃ��čςށA�D��I�ɉ����B<br/>
            /// ���ƃ����N�Ⴂ�����ɖ��ߔ�΂�����ł���B
            /// </summary>
            [Header("�`�[�����ł̊K��")]
            public CharacterRank rank;

            /// <summary>
            /// ���̐��l�ȏ�̓G����_���Ă��鑊�肪�^�[�Q�b�g�ɂȂ����ꍇ�A��U���̔��f�܂ł͑ҋ@�ɂȂ�
            /// ���̎��̔��f�ł���ς��ԃw�C�g������Α_���B(�_���܂����Ă鑊��ւ̃w�C�g�͉�����̂ŁA���ʂ͂��̎��̔��f�łׂ̂���_����)
            /// �l�q�f���A�݂����ȃX�e�[�g����邩��p��
            /// ���ȏ�ɑ_���Ă鑊�肩�A�l�q�f���Ă�L�����̏ꍇ�����w�C�g������悤�ɂ��悤�B
            /// </summary>
            [Header("�^�[�Q�b�g���")]
            [Tooltip("���̐��l�ȏ�̓G����_���Ă��鑊�肪�U���ΏۂɂȂ����ꍇ�A��U���̔��f�܂ł͑ҋ@�ɂȂ�")]
            public int targetingLimit;
        }

        /// <summary>
        /// AI�̐ݒ�B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterBrainStatus
        {
            /// <summary>
            /// AI�̔��f�Ԋu
            /// </summary>
            [Header("���f�Ԋu")]
            public float judgeInterval;

            /// <summary>
            /// �s���֘A�̐ݒ�f�[�^
            /// </summary>
            [Header("�s���ݒ�")]
            public BehaviorData[] actCondition;

            /// <summary>
            /// �U���ȊO�̍s�������f�[�^.
            /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
            /// </summary>
            [Header("�w�C�g�����f�[�^")]
            public TargetJudgeData[] hateCondition;

        }

        /// <summary>
        /// �s�����f���Ɏg�p����f�[�^�B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct BehaviorData
        {
            /// <summary>
            /// �s�����X�L�b�v���邽�߂̏����B
            /// </summary>
            [TabGroup(group: "AI����", tab: "�X�L�b�v����")]
            public SkipJudgeData skipData;

            /// <summary>
            /// �s���̏����B
            /// �Ώۂ̐w�c�Ɠ������w��ł���B
            /// </summary>
            [TabGroup(group: "AI����", tab: "�s������")]
            public ActJudgeData actCondition;

            /// <summary>
            /// �U���܂ރ^�[�Q�b�g�I���f�[�^
            /// �v�f�͈�����A���̑��蕡�G�ȏ����Ŏw��\
            /// ���Ɏw��Ȃ��ꍇ�̂݃w�C�g�œ���
            /// �����Ńw�C�g�ȊO�̏������w�肵���ꍇ�́A�s���܂ŃZ�b�g�Ō��߂�B
            /// </summary>
            [TabGroup(group: "AI����", tab: "�ΏۑI������")]
            public TargetJudgeData targetCondition;
        }

        /// <summary>
        /// ���f�Ɏg�p����f�[�^�B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct SkipJudgeData
        {
            /// <summary>
            /// �s��������X�L�b�v�������
            /// </summary>
            [Header("�s��������X�L�b�v�������")]
            public SkipJudgeCondition skipCondition;

            /// <summary>
            /// ���f�Ɏg�p���鐔�l�B
            /// �����ɂ���Ă�enum��ϊ��������������肷��B
            /// </summary>
            [Header("��ƂȂ�l")]
            public int judgeValue;

            /// <summary>
            /// �^�̏ꍇ�A���������]����
            /// �ȏ�͈ȓ��ɂȂ�Ȃ�
            /// </summary>
            [Header("����]�t���O")]
            public BitableBool isInvert;

        }

        /// <summary>
        /// ���f�Ɏg�p����f�[�^�B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct ActJudgeData
        {
            /// <summary>
            /// �s������
            /// </summary>
            [Header("�s������̏���")]
            public ActJudgeCondition judgeCondition;

            /// <summary>
            /// ���f�Ɏg�p���鐔�l�B
            /// �����ɂ���Ă�enum��ϊ��������������肷��B
            /// </summary>
            [Header("��ƂȂ�l")]
            public int judgeValue;

            /// <summary>
            /// �^�̏ꍇ�A���������]����
            /// �ȏ�͈ȓ��ɂȂ�Ȃ�
            /// </summary>
            [Header("����]�t���O")]
            public BitableBool isInvert;

            /// <summary>
            /// ���ꂪ�w��Ȃ��A�ȊO���ƃX�e�[�g�ύX���s���B
            /// ����čs�����f�̓X�L�b�v
            /// </summary>
            [Header("�ύX��̃��[�h�i�ύX����ꍇ�j")]
            public ActState stateChange;

            /// <summary>
            /// �Ώۂ̐w�c�敪
            /// �����w�肠��
            /// </summary>
            [Header("�`�F�b�N�Ώۂ̏���")]
            public TargetFilter filter;
        }

        /// <summary>
        /// �s�����f��A�s���̃^�[�Q�b�g��I������ۂɎg�p����f�[�^�B
        /// �w�C�g�ł�����ȊO�ł��\���͓̂���
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct TargetJudgeData
        {
            /// <summary>
            /// �^�[�Q�b�g�̔��f��B
            /// </summary>
            [Header("�^�[�Q�b�g���f�")]
            public TargetSelectCondition judgeCondition;

            /// <summary>
            /// �^�̏ꍇ�A���������]����
            /// �ȏ�͈ȓ��ɂȂ�Ȃ�
            /// </summary>
            [Header("����]�t���O")]
            public BitableBool isInvert;

            /// <summary>
            /// �Ώۂ̐w�c�敪
            /// �����w�肠��
            /// </summary>
            [Header("�`�F�b�N�Ώۂ̏���")]
            public TargetFilter filter;

            /// <summary>
            /// �g�p����s���̔ԍ��B
            /// �w��Ȃ� ( = -1)�̏ꍇ�͓G�̏������珟��Ɍ��߂�B(�w�C�g�Ō��߂��ꍇ��-1�̎w��Ȃ��ɂȂ�)
            /// �����łȂ��ꍇ�͂����܂Őݒ肷��B
            /// 
            /// ���邢�̓w�C�g�㏸�{���ɂȂ�B
            /// </summary>
            [Header("�w�C�g�{��or�g�p����s����No")]
            [Tooltip("�s���ԍ���-1���w�肵���ꍇ�AAI���Ώۂ̏�񂩂�s�������߂�")]
            public float useAttackOrHateNum;
        }

        /// <summary>
        /// �s��������Ώېݒ�����Ō����Ώۂ��t�B���^�[���邽�߂̍\����
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct TargetFilter
        {
            /// <summary>
            /// �Ώۂ̐w�c�敪
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ̐w�c")]
            [SerializeField]
            private CharacterSide targetType;

            /// <summary>
            /// �Ώۂ̓���
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ̓���")]
            [SerializeField]
            private CharacterFeature targetFeature;

            /// <summary>
            /// ���̃t���O���^�̎��A�S�����Ă͂܂��ĂȂ��ƃ_���B
            /// </summary>
            [Header("�����̔��f���@")]
            [SerializeField]
            private BitableBool isAndFeatureCheck;

            /// <summary>
            /// �Ώۂ̏�ԁi�o�t�A�f�o�t�j
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ����������")]
            [SerializeField]
            private SpecialEffect targetEffect;

            /// <summary>
            /// ���̃t���O���^�̎��A�S�����Ă͂܂��ĂȂ��ƃ_���B
            /// </summary>
            [Header("������ʂ̔��f���@")]
            [SerializeField]
            private BitableBool isAndEffectCheck;

            /// <summary>
            /// �Ώۂ̏�ԁi�����A�U���Ȃǁj
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ̏��")]
            [SerializeField]
            private ActState targetState;

            /// <summary>
            /// �Ώۂ̃C�x���g�󋵁i��_���[�W��^�����A�Ƃ��j�Ńt�B���^�����O
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ̃C�x���g")]
            [SerializeField]
            private BrainEventFlagType targetEvent;

            /// <summary>
            /// ���̃t���O���^�̎��A�S�����Ă͂܂��ĂȂ��ƃ_���B
            /// </summary>
            [Header("�C�x���g�̔��f���@")]
            [SerializeField]
            private BitableBool isAndEventCheck;

            /// <summary>
            /// �Ώۂ̎�_�����Ńt�B���^�����O
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ̎�_")]
            [SerializeField]
            private Element targetWeakPoint;

            /// <summary>
            /// �Ώۂ��g�������Ńt�B���^�����O
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ̎g�p����")]
            [SerializeField]
            private Element targetUseElement;

            /// <summary>
            /// �����ΏۃL�����N�^�[�̏����ɓ��Ă͂܂邩���`�F�b�N����B
            /// </summary>
            /// <param name="belong"></param>
            /// <param name="feature"></param>
            /// <returns></returns>
            [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
            public byte IsPassFilter(in CharacterData charaData)
            {
                // �_���폜�Ώۂ͏�ɖ����B
                if ( charaData.IsLogicalDelate() )
                {
                    return 0;
                }

                // and��or�œ�����������
                // ���Ă͂܂�Ȃ��Ȃ�A��B
                if ( this.isAndFeatureCheck == BitableBool.TRUE ? ((this.targetFeature != 0) && (this.targetFeature & charaData.solidData.feature) != this.targetFeature) :
                                                      ((this.targetFeature != 0) && (this.targetFeature & charaData.solidData.feature) == 0) )
                {
                    return 0;
                }

                // ������ʔ��f
                // ���Ă͂܂�Ȃ��Ȃ�A��B
                if ( this.isAndEffectCheck == BitableBool.TRUE ? ((this.targetEffect != 0) && (this.targetEffect & charaData.liveData.nowEffect) != this.targetEffect) :
                                          ((this.targetEffect != 0) && (this.targetEffect & charaData.liveData.nowEffect) == 0) )
                {
                    return 0;
                }

                // �C�x���g���f
                // ���Ă͂܂�Ȃ��Ȃ�A��B
                if ( this.isAndEventCheck == BitableBool.TRUE ? ((this.targetEvent != 0) && (this.targetEvent & charaData.liveData.brainEvent) != this.targetEvent) :
                                          ((this.targetEvent != 0) && (this.targetEvent & charaData.liveData.brainEvent) == 0) )
                {
                    return 0;
                }

                // �c��̏���������B
                if ( (this.targetType == 0 || ((this.targetType & charaData.liveData.belong) > 0)) && (this.targetState == 0 || ((this.targetState & charaData.liveData.actState) > 0))
                    && (this.targetWeakPoint == 0 || ((this.targetWeakPoint & charaData.solidData.weakPoint) > 0)) && (this.targetUseElement == 0 || ((this.targetUseElement & charaData.solidData.attackElement) > 0)) )
                {
                    return 1;
                }

                return 0;
            }

            #region �f�o�b�O�p

            /// <summary>
            /// IsPassFilter(CharacterData��)�̃f�o�b�O�p���\�b�h�B���s���������̏ڍׂ�Ԃ�
            /// </summary>
            public string DebugIsPassFilter(in CharacterData charaData)
            {
                System.Text.StringBuilder failedConditions = new();

                // 0. �_���폜�`�F�b�N
                if ( charaData.IsLogicalDelate() )
                {
                    _ = failedConditions.AppendLine($"[�_���폜�`�F�b�N�Ŏ��s]");
                    _ = failedConditions.AppendLine($"  ���R: �L�����N�^�[�͘_���폜����Ă��܂�");
                    _ = failedConditions.AppendLine($"  CharacterID: {charaData.hashCode}"); // ID������ꍇ
                    return failedConditions.ToString();
                }

                // 1. ������������
                if ( this.targetFeature != 0 )
                {
                    bool featureFailed = false;
                    string failureReason = "";

                    if ( this.isAndFeatureCheck == BitableBool.TRUE )
                    {
                        // AND�����F�S�Ă̓������K�v
                        if ( (this.targetFeature & charaData.solidData.feature) != this.targetFeature )
                        {
                            featureFailed = true;
                            CharacterFeature missingFeatures = this.targetFeature & ~charaData.solidData.feature;
                            failureReason = $"AND�������s - �K�v�ȓ������s��: {missingFeatures}";
                        }
                    }
                    else
                    {
                        // OR�����F�����ꂩ�̓������K�v
                        if ( (this.targetFeature & charaData.solidData.feature) == 0 )
                        {
                            featureFailed = true;
                            failureReason = "OR�������s - ��v��������Ȃ�";
                        }
                    }

                    if ( featureFailed )
                    {
                        _ = failedConditions.AppendLine($"[���������Ŏ��s]");
                        _ = failedConditions.AppendLine($"  �t�B�[���h: targetFeature");
                        _ = failedConditions.AppendLine($"  ���Ғl: {this.targetFeature} (0x{this.targetFeature:X})");
                        _ = failedConditions.AppendLine($"  ���ۂ̒l: {charaData.solidData.feature} (0x{charaData.solidData.feature:X})");
                        _ = failedConditions.AppendLine($"  ������@: {(this.isAndFeatureCheck == BitableBool.TRUE ? "AND" : "OR")}");
                        _ = failedConditions.AppendLine($"  ���R: {failureReason}");
                        _ = failedConditions.AppendLine();
                        return failedConditions.ToString();
                    }
                }

                // 2. ������ʔ��f
                if ( this.targetEffect != 0 )
                {
                    bool effectFailed = false;
                    string failureReason = "";

                    if ( this.isAndEffectCheck == BitableBool.TRUE )
                    {
                        // AND�����F�S�Ă̌��ʂ��K�v
                        if ( (this.targetEffect & charaData.liveData.nowEffect) != this.targetEffect )
                        {
                            effectFailed = true;
                            SpecialEffect missingEffects = this.targetEffect & ~charaData.liveData.nowEffect;
                            failureReason = $"AND�������s - �K�v�Ȍ��ʂ��s��: {missingEffects}";
                        }
                    }
                    else
                    {
                        // OR�����F�����ꂩ�̌��ʂ��K�v
                        if ( (this.targetEffect & charaData.liveData.nowEffect) == 0 )
                        {
                            effectFailed = true;
                            failureReason = "OR�������s - ��v������ʂȂ�";
                        }
                    }

                    if ( effectFailed )
                    {
                        _ = failedConditions.AppendLine($"[������ʏ����Ŏ��s]");
                        _ = failedConditions.AppendLine($"  �t�B�[���h: targetEffect");
                        _ = failedConditions.AppendLine($"  ���Ғl: {this.targetEffect} (0x{this.targetEffect:X})");
                        _ = failedConditions.AppendLine($"  ���ۂ̒l: {charaData.liveData.nowEffect} (0x{charaData.liveData.nowEffect:X})");
                        _ = failedConditions.AppendLine($"  ������@: {(this.isAndEffectCheck == BitableBool.TRUE ? "AND" : "OR")}");
                        _ = failedConditions.AppendLine($"  ���R: {failureReason}");
                        _ = failedConditions.AppendLine();
                        return failedConditions.ToString();
                    }
                }

                // 3. �C�x���g���f
                if ( this.targetEvent != 0 )
                {
                    bool eventFailed = false;
                    string failureReason = "";

                    if ( this.isAndEventCheck == BitableBool.TRUE )
                    {
                        // AND�����F�S�ẴC�x���g���K�v
                        if ( (this.targetEvent & charaData.liveData.brainEvent) != this.targetEvent )
                        {
                            eventFailed = true;
                            BrainEventFlagType missingEvents = this.targetEvent & ~charaData.liveData.brainEvent;
                            failureReason = $"AND�������s - �K�v�ȃC�x���g���s��: {missingEvents}";
                        }
                    }
                    else
                    {
                        // OR�����F�����ꂩ�̃C�x���g���K�v
                        if ( (this.targetEvent & charaData.liveData.brainEvent) == 0 )
                        {
                            eventFailed = true;
                            failureReason = "OR�������s - ��v����C�x���g�Ȃ�";
                        }
                    }

                    if ( eventFailed )
                    {
                        _ = failedConditions.AppendLine($"[�C�x���g�����Ŏ��s]");
                        _ = failedConditions.AppendLine($"  �t�B�[���h: targetEvent");
                        _ = failedConditions.AppendLine($"  ���Ғl: {this.targetEvent} (0x{this.targetEvent:X})");
                        _ = failedConditions.AppendLine($"  ���ۂ̒l: {charaData.liveData.brainEvent} (0x{charaData.liveData.brainEvent:X})");
                        _ = failedConditions.AppendLine($"  ������@: {(this.isAndEventCheck == BitableBool.TRUE ? "AND" : "OR")}");
                        _ = failedConditions.AppendLine($"  ���R: {failureReason}");
                        _ = failedConditions.AppendLine();
                        return failedConditions.ToString();
                    }
                }

                // 4. �c��̏����i�ʃ`�F�b�N�j
                List<string> remainingFailures = new();

                // �w�c�`�F�b�N
                if ( this.targetType != 0 && (this.targetType & charaData.liveData.belong) == 0 )
                {
                    remainingFailures.Add($"  - targetType: ���Ғl={this.targetType} (0x{this.targetType:X}), ���ۂ̒l={charaData.liveData.belong} (0x{charaData.liveData.belong:X})");
                }

                // ��ԃ`�F�b�N
                if ( this.targetState != 0 && (this.targetState & charaData.liveData.actState) == 0 )
                {
                    remainingFailures.Add($"  - targetState: ���Ғl={this.targetState} (0x{this.targetState:X}), ���ۂ̒l={charaData.liveData.actState} (0x{charaData.liveData.actState:X})");
                }

                // ��_�`�F�b�N
                if ( this.targetWeakPoint != 0 && (this.targetWeakPoint & charaData.solidData.weakPoint) == 0 )
                {
                    remainingFailures.Add($"  - targetWeakPoint: ���Ғl={this.targetWeakPoint} (0x{this.targetWeakPoint:X}), ���ۂ̒l={charaData.solidData.weakPoint} (0x{charaData.solidData.weakPoint:X})");
                }

                // �g�p�����`�F�b�N
                if ( this.targetUseElement != 0 && (this.targetUseElement & charaData.solidData.attackElement) == 0 )
                {
                    remainingFailures.Add($"  - targetUseElement: ���Ғl={this.targetUseElement} (0x{this.targetUseElement:X}), ���ۂ̒l={charaData.solidData.attackElement} (0x{charaData.solidData.attackElement:X})");
                }

                if ( remainingFailures.Count > 0 )
                {
                    _ = failedConditions.AppendLine($"[���̑��̏����Ŏ��s]");
                    foreach ( string failure in remainingFailures )
                    {
                        _ = failedConditions.AppendLine(failure);
                    }

                    return failedConditions.ToString();
                }

                // �S�����p�X�����ꍇ
                return "�S�Ă̏������p�X���܂���";
            }

            /// <summary>
            /// CharacterData�̏�Ԃ��܂߂��ڍׂȃf�o�b�O�����o��
            /// </summary>
            public string DebugIsPassFilterWithCharacterInfo(in CharacterData charaData)
            {
                System.Text.StringBuilder result = new();

                // ��{�I�ȃt�B���^����
                _ = result.AppendLine("=== �t�B���^�`�F�b�N���� ===");
                _ = result.AppendLine(this.DebugIsPassFilter(charaData));

                // �L�����N�^�[�̌��݂̏�Ԃ��o��
                _ = result.AppendLine("\n=== �L�����N�^�[�̌��ݏ�� ===");
                _ = result.AppendLine("[SolidData]");
                _ = result.AppendLine($"  feature: {charaData.solidData.feature} (0x{charaData.solidData.feature:X})");
                _ = result.AppendLine($"  weakPoint: {charaData.solidData.weakPoint} (0x{charaData.solidData.weakPoint:X})");
                _ = result.AppendLine($"  attackElement: {charaData.solidData.attackElement} (0x{charaData.solidData.attackElement:X})");

                _ = result.AppendLine("\n[LiveData]");
                _ = result.AppendLine($"  belong: {charaData.liveData.belong} (0x{charaData.liveData.belong:X})");
                _ = result.AppendLine($"  actState: {charaData.liveData.actState} (0x{charaData.liveData.actState:X})");
                _ = result.AppendLine($"  nowEffect: {charaData.liveData.nowEffect} (0x{charaData.liveData.nowEffect:X})");
                _ = result.AppendLine($"  brainEvent: {charaData.liveData.brainEvent} (0x{charaData.liveData.brainEvent:X})");
                _ = result.AppendLine($"  �_���폜���: {(charaData.IsLogicalDelate() ? "�폜�ς�" : "�L��")}");

                // �t�B���^�̐ݒ�l���o��
                _ = result.AppendLine("\n=== �t�B���^�ݒ� ===");
                _ = result.AppendLine($"  targetType: {this.targetType} (0x{this.targetType:X})");
                _ = result.AppendLine($"  targetFeature: {this.targetFeature} (0x{this.targetFeature:X}) [{this.isAndFeatureCheck}]");
                _ = result.AppendLine($"  targetEffect: {this.targetEffect} (0x{this.targetEffect:X}) [{this.isAndEffectCheck}]");
                _ = result.AppendLine($"  targetState: {this.targetState} (0x{this.targetState:X})");
                _ = result.AppendLine($"  targetEvent: {this.targetEvent} (0x{this.targetEvent:X}) [{this.isAndEventCheck}]");
                _ = result.AppendLine($"  targetWeakPoint: {this.targetWeakPoint} (0x{this.targetWeakPoint:X})");
                _ = result.AppendLine($"  targetUseElement: {this.targetUseElement} (0x{this.targetUseElement:X})");

                return result.ToString();
            }

            /// <summary>
            /// ����������̃V�~�����[�V�����i�ǂ̒l�Ȃ�ʂ邩��񎦁j
            /// </summary>
            public string SimulatePassConditions(in CharacterData charaData)
            {
                System.Text.StringBuilder result = new();
                _ = result.AppendLine("=== �ʉߏ����V�~�����[�V���� ===");

                // �e�����ɂ��āA�ǂ�����Βʂ邩���
                if ( charaData.IsLogicalDelate() )
                {
                    _ = result.AppendLine("x �_���폜����Ă��邽�߁A�ǂ�ȏ����ł��ʉߕs��");
                    return result.ToString();
                }

                // ��������
                if ( this.targetFeature != 0 )
                {
                    if ( this.isAndFeatureCheck == BitableBool.TRUE )
                    {
                        CharacterFeature required = this.targetFeature;
                        CharacterFeature current = charaData.solidData.feature;
                        CharacterFeature missing = required & ~current;
                        if ( missing != 0 )
                        {
                            _ = result.AppendLine($"x ��������(AND): �ǉ��ŕK�v�ȃt���O = {missing} (0x{missing:X})");
                        }
                        else
                        {
                            _ = result.AppendLine($"o ��������(AND): �����𖞂����Ă��܂�");
                        }
                    }
                    else
                    {
                        if ( (this.targetFeature & charaData.solidData.feature) == 0 )
                        {
                            _ = result.AppendLine($"x ��������(OR): �����ꂩ�̃t���O���K�v = {this.targetFeature} (0x{this.targetFeature:X})");
                        }
                        else
                        {
                            _ = result.AppendLine($"o ��������(OR): �����𖞂����Ă��܂�");
                        }
                    }
                }

                // ���l�ɑ��̏������`�F�b�N...

                return result.ToString();
            }

            public void Deconstruct(
    out CharacterSide targetType,
    out CharacterFeature targetFeature,
    out BitableBool isAndFeatureCheck,
    out SpecialEffect targetEffect,
    out BitableBool isAndEffectCheck,
    out ActState targetState,
    out BrainEventFlagType targetEvent,
    out BitableBool isAndEventCheck,
    out Element targetWeakPoint,
    out Element targetUseElement)
            {
                targetType = this.targetType;
                targetFeature = this.targetFeature;
                isAndFeatureCheck = this.isAndFeatureCheck;
                targetEffect = this.targetEffect;
                isAndEffectCheck = this.isAndEffectCheck;
                targetState = this.targetState;
                targetEvent = this.targetEvent;
                isAndEventCheck = this.isAndEventCheck;
                targetWeakPoint = this.targetWeakPoint;
                targetUseElement = this.targetUseElement;
            }

            #endregion
        }

        /// <summary>
        /// �U���̃X�e�[�^�X�B
        /// ����̓X�e�[�^�X��Scriptable�Ɏ������Ă����̂ŃG�t�F�N�g�f�[�^�Ƃ��̎Q�ƌ^������Ă����B
        /// �O��g�p�������ԁA�Ƃ����L�^���邽�߂ɁA�L�����N�^�[���ɕʓr�����N�����Ǘ���񂪕K�v�B
        /// ����Job�V�X�e���Ŏg�p���Ȃ��\���̂͂Ȃ�ׂ����������C�A�E�g���œK������B�l�C�e�B�u�R�[�h�Ƃ̘A�g���C�ɂ��Ȃ��Ă�������B
        /// ���ۂɃQ�[���ɑg�ݍ��ގ��͍U���ȊO�̍s���ɂ��Ή��ł���悤�ɂ��邩�B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Auto)]
        public struct AttackData
        {
            /// <summary>
            /// �U���{���B
            /// �����郂�[�V�����l
            /// </summary>
            [Header("�U���{���i���[�V�����l�j")]
            public float motionValue;
        }

        /// <summary>
        /// �L�����̍s���X�e�[�^�X�B
        /// �ړ����x�ȂǁB
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct MoveStatus
        {
            /// <summary>
            /// �ʏ�̈ړ����x
            /// </summary>
            [Header("�ʏ�ړ����x")]
            public int moveSpeed;

            /// <summary>
            /// ���s���x�B������������
            /// </summary>
            [Header("���s���x")]
            public int walkSpeed;

            /// <summary>
            /// �_�b�V�����x
            /// </summary>
            [Header("�_�b�V�����x")]
            public int dashSpeed;

            /// <summary>
            /// �W�����v�̍����B
            /// </summary>
            [Header("�W�����v�̍���")]
            public int jumpHeight;
        }

        #endregion �\���̒�`

        #region �V���A���C�Y�\�ȃf�B�N�V���i���̒�`

        /// <summary>
        /// ActState���L�[��CharacterBrainStatus���l�̃f�B�N�V���i��
        /// </summary>
        [Serializable]
        public class ActStateBrainDictionary : SerializableDictionary<ActState, CharacterBrainStatus>
        {
        }

        #endregion

        #region ���s���L�����N�^�[�f�[�^�֘A�̍\���̒�`

        /// <summary>
        /// Job�V�X�e���Ŏg�p����L�����N�^�[�f�[�^�\���́B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterData : IDisposable, ILogicalDelate
        {
            /// <summary>
            /// �V�����L�����N�^�[�f�[�^���擾����B
            /// </summary>
            /// <param name="status"></param>
            /// <param name="gameObject"></param>
            public CharacterData(BrainStatus status, GameObject gameObject)
            {
                this.brainData = new NativeHashMap<int, CharacterBrainStatusForJob>(status.brainData.Count, Allocator.Persistent);

                foreach ( KeyValuePair<ActState, CharacterBrainStatus> item in status.brainData )
                {
                    CharacterBrainStatusForJob newData = new(item.Value, Allocator.Persistent);
                    this.brainData.Add((int)item.Key, newData);
                }

                this.hashCode = gameObject.GetHashCode();
                this.liveData = new CharacterUpdateData(status.baseData, gameObject.transform.position);
                this.solidData = status.solidData;
                this.targetingCount = 0;
                // �ŏ��̓}�C�i�X��10000�����邱�Ƃł���������悤��
                this.lastJudgeTime = -10000;

                this.personalHate = new NativeHashMap<int, int>(7, Allocator.Persistent);
                this.shortRangeCharacter = new UnsafeList<int>(7, Allocator.Persistent);

                this.moveJudgeInterval = status.moveJudgeInterval;
                this.lastMoveJudgeTime = 0;// �ǂ����s�����f���ɐU���������

                // �ŏ��͘_���폜�t���O�Ȃ��B
                this.isLogicalDelate = BitableBool.FALSE;
            }

            /// <summary>
            /// �Œ�̃f�[�^�B
            /// </summary>
            public SolidData solidData;

            /// <summary>
            /// �L������AI�̐ݒ�B(Job�o�[�W����)
            /// ���[�h���ƂɃ��[�hEnum��int�ϊ����������C���f�b�N�X�ɂ����z��ɂȂ�B
            /// </summary>
            public NativeHashMap<int, CharacterBrainStatusForJob> brainData;

            /// <summary>
            /// �X�V���ꂤ��f�[�^�B
            /// </summary>
            public CharacterUpdateData liveData;

            /// <summary>
            /// ������_���Ă�G�̐��B
            /// �{�X���w�����͖����ł悳����
            /// ���U�����Ă����U�����I������ʂ̃^�[�Q�b�g��_���B
            /// ���̃^�C�~���O�Ŋ��肱�߂������荞��
            /// �����܂Ńw�C�g�l�����炷�����ŁB��U�ҋ@�ɂȂ��āA�w�C�g���邾���Ȃ̂ŉ���ꂽ�牣��Ԃ���
            /// ������ԈȊO�Ȃ牓���ɂȂ邵�A�������łȂ���ԃw�C�g�����Ȃ�U�����āA���̎��͉����ɂȂ�
            /// </summary>
            public int targetingCount;

            /// <summary>
            /// �Ō�ɔ��f�������ԁB
            /// </summary>
            public float lastJudgeTime;

            /// <summary>
            /// �Ō�Ɉړ����f�������ԁB
            /// </summary>
            public float lastMoveJudgeTime;

            /// <summary>
            /// �L�����N�^�[�̃n�b�V���l��ۑ����Ă����B
            /// </summary>
            public int hashCode;

            /// <summary>
            /// �U�����Ă�������Ƃ��A���ړI�ȏ����ɓ��Ă͂܂�������̃w�C�g�����L�^����B
            /// </summary>
            public NativeHashMap<int, int> personalHate;

            /// <summary>
            /// �߂��ɂ���L�����N�^�[�̋L�^�B
            /// ����̓Z���T�[�Œf���I�Ɏ擾����Q�l�l�B
            /// �v�f�������7~10�̗\��
            /// </summary>
            public UnsafeList<int> shortRangeCharacter;

            /// <summary>
            /// AI�̈ړ����f�Ԋu
            /// </summary>
            [Header("�ړ����f�Ԋu")]
            public float moveJudgeInterval;

            /// <summary>
            /// �_���폜�t���O�B
            /// </summary>
            /// 
            private BitableBool isLogicalDelate;

            /// <summary>
            /// NativeContainer���܂ރ����o�[��j���B
            /// AIManager���ӔC�������Ĕj������B
            /// </summary>
            public void Dispose()
            {
                this.brainData.Dispose();
                this.personalHate.Dispose();
                this.shortRangeCharacter.Dispose();
            }

            /// <summary>
            /// �_���폜�t���O�̊m�F�B
            /// </summary>
            /// <returns>�^�ł���Θ_���폜�ς�</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsLogicalDelate()
            {
                return this.isLogicalDelate == BitableBool.TRUE;
            }

            /// <summary>
            /// �_���폜�����s����B
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void LogicalDelete()
            {
                this.isLogicalDelate = BitableBool.TRUE;
            }
        }

        /// <summary>
        /// AI�̐ݒ�B�iJob�V�X�e���d�l�j
        /// �X�e�[�^�X��CharacterBrainStatus����ڐA����B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterBrainStatusForJob : IDisposable
        {
            /// <summary>
            /// AI�̔��f�Ԋu
            /// </summary>
            [Header("���f�Ԋu")]
            public float judgeInterval;

            /// <summary>
            /// �s�������f�[�^
            /// </summary>
            [Header("�s�������f�[�^")]
            public NativeArray<BehaviorData> actCondition;

            /// <summary>
            /// �U���ȊO�̍s�������f�[�^.
            /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
            /// </summary>
            [Header("�w�C�g�����f�[�^")]
            public NativeArray<TargetJudgeData> hateCondition;

            /// <summary>
            /// NativeArray���\�[�X���������
            /// </summary>
            public void Dispose()
            {
                if ( this.actCondition.IsCreated )
                {
                    this.actCondition.Dispose();
                }

                if ( this.hateCondition.IsCreated )
                {
                    this.hateCondition.Dispose();
                }
            }

            /// <summary>
            /// �I���W�i����CharacterBrainStatus����f�[�^�𖾎��I�ɈڐA
            /// </summary>
            /// <param name="source">�ڐA���̃L�����N�^�[�u���C���X�e�[�^�X</param>
            /// <param name="allocator">NativeArray�Ɏg�p����A���P�[�^</param>
            public CharacterBrainStatusForJob(in CharacterBrainStatus source, Allocator allocator)
            {

                // ��{�v���p�e�B���R�s�[
                this.judgeInterval = source.judgeInterval;

                // �z���V�����쐬
                this.actCondition = source.actCondition != null
                    ? new NativeArray<BehaviorData>(source.actCondition, allocator)
                    : new NativeArray<BehaviorData>(0, allocator);

                this.hateCondition = source.hateCondition != null
                    ? new NativeArray<TargetJudgeData>(source.hateCondition, allocator)
                    : new NativeArray<TargetJudgeData>(0, allocator);
            }

        }

        /// <summary>
        /// �X�V�����L�����N�^�[�̏��B
        /// ��Ԉُ�Ƃ��o�t������Ď��Ԍp���̏I���܂�Job�Ō��邩�B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterUpdateData
        {
            /// <summary>
            /// �ő�̗�
            /// </summary>
            public int maxHp;

            /// <summary>
            /// �̗�
            /// </summary>
            public int currentHp;

            /// <summary>
            /// �ő喂��
            /// </summary>
            public int maxMp;

            /// <summary>
            /// ����
            /// </summary>
            public int currentMp;

            /// <summary>
            /// HP�̊���
            /// </summary>
            public int hpRatio;

            /// <summary>
            /// MP�̊���
            /// </summary>
            public int mpRatio;

            /// <summary>
            /// �e�����̊�b�U����
            /// </summary>
            public ElementalStatus atk;

            /// <summary>
            /// �S�U���͂̉��Z�B
            /// </summary>
            public int dispAtk;

            /// <summary>
            /// �e�����̊�b�h���
            /// </summary>
            public ElementalStatus def;

            /// <summary>
            /// �S�h��͂̉��Z�B
            /// </summary>
            public int dispDef;

            /// <summary>
            /// ���݈ʒu�B
            /// </summary>
            public Vector2 nowPosition;

            /// <summary>
            /// ���݂̃L�����N�^�[�̏���
            /// </summary>
            public CharacterSide belong;

            /// <summary>
            /// ���݂̍s���󋵁B
            /// ���f�Ԋu�o�߂�����X�V�H
            /// �U�����ꂽ�肵����X�V�H
            /// ���ƒ��Ԃ���̖��߂Ƃ��ł��X�V���Ă�������
            /// 
            /// �ړ��Ƃ�������AI�̓��삪�ς��B
            /// �����̏ꍇ�͓G�̋������Q�Ƃ��đ��肪���Ȃ��Ƃ���ɓ����悤�ƍl������
            /// </summary>
            public ActState actState;

            /// <summary>
            /// �L��������_���[�W��^�����A�Ȃǂ̃C�x���g���i�[����ꏊ�B
            /// </summary>
            public int brainEventBit;

            /// <summary>
            /// �o�t��f�o�t�Ȃǂ̌��݂̌���
            /// </summary>
            public SpecialEffect nowEffect;

            /// <summary>
            /// AI�����҂̍s����F�����邽�߂̃C�x���g�t���O�B
            /// �񋓌^AIEventFlagType�@�̃r�b�g���Z�Ɏg���B
            /// AIManager���t���O�Ǘ��͂��Ă����
            /// </summary>
            public BrainEventFlagType brainEvent;

            /// <summary>
            /// ������CharacterUpdateData��CharacterBaseData�̒l��K�p����
            /// </summary>
            /// <param name="baseData">�K�p���̃x�[�X�f�[�^</param>
            public CharacterUpdateData(in CharacterBaseData baseData, Vector2 initialPosition)
            {
                // �U���͂Ɩh��͂��X�V
                this.atk = baseData.baseAtk;
                this.def = baseData.baseDef;

                this.maxHp = baseData.hp;
                this.maxMp = baseData.mp;
                this.currentHp = baseData.hp;
                this.currentMp = baseData.mp;
                this.hpRatio = 100;
                this.mpRatio = 100;

                this.belong = baseData.initialBelong;

                this.nowPosition = initialPosition;

                this.actState = baseData.initialMove;
                this.brainEventBit = 0;

                this.dispAtk = this.atk.ReturnSum();
                this.dispDef = this.def.ReturnSum();

                this.nowEffect = SpecialEffect.�Ȃ�;
                this.brainEvent = BrainEventFlagType.None;
            }
        }

        #endregion �L�����N�^�[�f�[�^�֘A�̍\���̒�`

        /// <summary>
        /// �L�����̃x�[�X�A�Œ蕔���̃f�[�^�B
        /// ����͒��ڎd�l�͂����A�R�s�[���Ċe�L�����ɓn���Ă�����B
        /// </summary>
        [Header("�L�����N�^�[�̊�{�f�[�^")]
        public CharacterBaseData baseData;

        /// <summary>
        /// �Œ�̃f�[�^�B
        /// </summary>
        [Header("�Œ�f�[�^")]
        public SolidData solidData;

        /// <summary>
        /// �L������AI�̐ݒ�B
        /// ���[�h�i�U���ⓦ���Ȃǂ̏�ԁj���ƂɃ��[�h��Enum�� int �ϊ����������L�[�ɂ���HashMap�ɂȂ�B
        /// ActStateBrainDictionary�̓V���A���C�Y�\��Dictionary�B
        /// </summary>
        [Header("�L����AI�̐ݒ�")]
        public ActStateBrainDictionary brainData;

        /// <summary>
        /// �ړ����x�Ȃǂ̃f�[�^
        /// </summary>
        [Header("�ړ��X�e�[�^�X")]
        public MoveStatus moveStatus;

        /// <summary>
        /// �e�U���̍U���͂Ȃǂ̐ݒ�B
        /// Job�ɓ���Ȃ��̂ōU���G�t�F�N�g�����������Ă����B
        /// </summary>
        [Header("�U���f�[�^�ꗗ")]
        public AttackData[] attackData;

        /// <summary>
        /// AI�̈ړ����f�Ԋu
        /// </summary>
        [Header("�ړ����f�Ԋu")]
        public float moveJudgeInterval;

    }
}

