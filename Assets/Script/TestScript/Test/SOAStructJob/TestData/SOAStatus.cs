using MyTool.Collections;
using Sirenix.OdinInspector;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;
using static CharacterController.AIManager;
using Unity.Mathematics;
using static CharacterController.BrainStatus;
using CharacterController;


namespace TestScript.SOATest
{
    /// <summary>
    /// �g�p��ʂɊ�Â��ăf�[�^���\���̂ɐ؂蕪���Ă���B<br/>
    /// 
    /// ���̃N���X�ł̓L�����N�^�[�̎�ނ��Ƃ̐ݒ�f�[�^���`���Ă���B<br/>
    /// ������X�e�[�^�X�f�[�^�B<br/>
    /// �ϕ����i���W��HP�j�A�L�����N�^�[�̈ӎv����Ɏg�p���镔���i���f�������j��Job�V�X�e���Ŏg�p����O��Œl�^�݂̂ō\���B<br/>
    /// ���̕�����ScriptableObject�������s�ς̋��L�f�[�^�Ƃ��Ď����Ă���΂������߁A�Q�ƌ^(�G�t�F�N�g�̃f�[�^�Ȃ�)���g���B<br/>
    /// 
    /// �Ǘ����j
    /// �X�V�����f�[�^�FSOA�\���̃L�����f�[�^�ۊǌɂŊǗ�
    /// �Œ�f�[�^�i�l�^�j�F�L�����̎�ނ��ƂɃL�����f�[�^�����߂��z���Scriptable�ō쐬���AMemCpy��NativeArray�Ɉ�������B
    /// �@�@�@�@�@�@        �V�X�^�[����݂����ȍ�킪�ύX������́A�ő�l�Ŏ��O�Ƀo�b�t�@���Ă����B
    /// �Œ�f�[�^�i�Q�ƌ^�j�F�L�����̎�ނ��ƂɃL�����f�[�^�����߂��z���Scriptable�Ŏ����Ă����B
    /// 
    /// ���ʁF�L�����̎�ނ��ƂɃL�����f�[�^�����߂��z��ɂ̓L����ID�ŃA�N�Z�X����B
    /// </summary>
    [CreateAssetMenu(fileName = "SOAStatus", menuName = "Scriptable Objects/SOAStatus")]
    public class SOAStatus : SerializedScriptableObject
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
        public enum CharacterSide
        {
            �v���C���[ = 0,// ����
            ���� = 1,// ��ʓI�ȓG
            ���̑� = 2,// ����ȊO
            �w��Ȃ� = 3
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
        /// SoA OK
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
        /// �L�����̍s���i���s���x�Ƃ��j�̃X�e�[�^�X�B
        /// �ړ����x�ȂǁB
        /// 16Byte
        /// SoA Ok
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

        #region ���s���L�����N�^�[�f�[�^�֘A�̍\���̒�`

        /// <summary>
        /// BaseImfo region - �L�����N�^�[�̊�{���iHP�AMP�A�ʒu�j
        /// �T�C�Y: 32�o�C�g
        /// �p�r: ���t���[���X�V������{�X�e�[�^�X(ID�ȊO)
        /// SoA OK
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterBaseInfo
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
            /// ���݈ʒu
            /// </summary>
            public float2 nowPosition;

            /// <summary>
            /// HP/MP�������X�V����
            /// </summary>
            public void UpdateRatios()
            {
                hpRatio = maxHp > 0 ? (currentHp * 100) / maxHp : 0;
                mpRatio = maxMp > 0 ? (currentMp * 100) / maxMp : 0;
            }

            /// <summary>
            /// CharacterBaseData�����{����ݒ�
            /// </summary>
            public CharacterBaseInfo(in CharacterBaseData baseData, Vector2 initialPosition)
            {
                maxHp = baseData.hp;
                maxMp = baseData.mp;
                currentHp = baseData.hp;
                currentMp = baseData.mp;
                hpRatio = 100;
                mpRatio = 100;
                nowPosition = initialPosition;
            }
        }

        /// <summary>
        /// ��ɕς��Ȃ��f�[�^���i�[����\���́B
        /// BaseData�Ƃ̈Ⴂ�́A�������ȍ~�p�ɂɎQ�Ƃ���K�v�����邩�B
        /// SoA OK 20Byte
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct SolidData
        {

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
        /// �Q�ƕp�x�����Ȃ��A�����ĘA���Q�Ƃ���Ȃ��f�[�^���W�߂��\���́B
        /// 16byte
        /// </summary>
        public struct CharaColdLog
        {
            /// <summary>
            /// �L�����N�^�[��ID
            /// </summary>
            public readonly int characterID;

            /// <summary>
            /// �L�����N�^�[�̃n�b�V���l��ۑ����Ă����B
            /// </summary>
            public int hashCode;

            /// <summary>
            /// �Ō�ɔ��f�������ԁB
            /// </summary>
            public float lastJudgeTime;

            /// <summary>
            /// �Ō�Ɉړ����f�������ԁB
            /// </summary>
            public float lastMoveJudgeTime;

            public CharaColdLog(SOAStatus status, GameObject gameObject)
            {
                characterID = status.characterID;
                hashCode = gameObject.GetHashCode();
                // �ŏ��̓}�C�i�X��10000�����邱�Ƃł���������悤��
                this.lastJudgeTime = -10000;
                this.lastMoveJudgeTime = -10000;
            }

        }

        /// <summary>
        /// �U���͂̃f�[�^
        /// �T�C�Y: 32�o�C�g
        /// �p�r: �퓬�v�Z���ɃA�N�Z�X
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterAtkStatus
        {
            /// <summary>
            /// �e�����̊�b�U����
            /// </summary>
            public ElementalStatus atk;

            /// <summary>
            /// �S�U���͂̉��Z
            /// </summary>
            public int dispAtk;

            /// <summary>
            /// �\���p�U���͂��X�V
            /// </summary>
            public void UpdateDisplayAttack()
            {
                dispAtk = atk.ReturnSum();
            }

            /// <summary>
            /// CharacterBaseData����퓬�X�e�[�^�X��ݒ�
            /// </summary>
            public CharacterAtkStatus(in CharacterBaseData baseData)
            {
                atk = baseData.baseAtk;
                dispAtk = atk.ReturnSum();
            }
        }

        /// <summary>
        /// �h��͂̃f�[�^
        /// �T�C�Y: 32�o�C�g
        /// �p�r: �퓬�v�Z���ɃA�N�Z�X
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterDefStatus
        {

            /// <summary>
            /// �e�����̊�b�h���
            /// </summary>
            public ElementalStatus def;

            /// <summary>
            /// �S�h��͂̉��Z
            /// </summary>
            public int dispDef;

            /// <summary>
            /// �\���p�h��͂��X�V
            /// </summary>
            public void UpdateDisplayDefense()
            {
                dispDef = def.ReturnSum();
            }

            /// <summary>
            /// CharacterBaseData����퓬�X�e�[�^�X��ݒ�
            /// </summary>
            public CharacterDefStatus(in CharacterBaseData baseData)
            {
                def = baseData.baseDef;
                dispDef = def.ReturnSum();
            }
        }

        /// <summary>
        /// StateImfo region - �L�����N�^�[�̏�ԏ��
        /// �T�C�Y: 16�o�C�g�i1�L���b�V�����C����25%�j
        /// �p�r: AI���f�A��ԊǗ�
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterStateInfo
        {
            /// <summary>
            /// ���݂̃L�����N�^�[�̏���
            /// </summary>
            public CharacterSide belong;

            /// <summary>
            /// ���݂̍s����
            /// ���f�Ԋu�o�߂�����X�V�H
            /// �U�����ꂽ�肵����X�V�H
            /// ���ƒ��Ԃ���̖��߂Ƃ��ł��X�V���Ă�������
            /// 
            /// �ړ��Ƃ�������AI�̓��삪�ς��B
            /// �����̏ꍇ�͓G�̋������Q�Ƃ��đ��肪���Ȃ��Ƃ���ɓ����悤�ƍl������
            /// </summary>
            public ActState actState;

            /// <summary>
            /// �o�t��f�o�t�Ȃǂ̌��݂̌���
            /// </summary>
            public SpecialEffect nowEffect;

            /// <summary>
            /// AI�����҂̍s����F�����邽�߂̃C�x���g�t���O
            /// �񋓌^AIEventFlagType�@�̃r�b�g���Z�Ɏg��
            /// AIManager���t���O�Ǘ��͂��Ă����
            /// </summary>
            public BrainEventFlagType brainEvent;

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
            /// ��Ԃ����Z�b�g�i���������p�j
            /// </summary>
            public void ResetStates()
            {
                nowEffect = SpecialEffect.�Ȃ�;
                brainEvent = BrainEventFlagType.None;
            }

            /// <summary>
            /// CharacterBaseData�����ԏ���ݒ�
            /// </summary>
            public CharacterStateInfo(in CharacterBaseData baseData)
            {
                belong = baseData.initialBelong;
                actState = baseData.initialMove;
                nowEffect = SpecialEffect.�Ȃ�;
                brainEvent = BrainEventFlagType.None;
                targetingCount = 0;
            }
        }


        #endregion �L�����N�^�[�f�[�^�֘A�̍\���̒�`

        #region SoA�ΏۊO

        #region ���f�֘A(Job�g�p)

        /// <summary>
        /// AI�̐ݒ�B
        /// �v�C���B�C���X�y�N�^�Ŏg�������Ȃ炱�̂܂܂ɂ��ĕϊ����悤��
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct BrainSetting
        {

            /// <summary>
            /// �s���֘A�̐ݒ�f�[�^
            /// </summary>
            [Header("�s���ݒ�")]
            public BehaviorData[] actCondition;



        }

        /// <summary>
        /// AI�̐ݒ�B�iJob�V�X�e���d�l�j
        /// �X�e�[�^�X��CharacterSOAStatus����ڐA����B
        /// �^�p�@�Ƃ��Ă�ReadOnly�ŁA�eJob�̎��Ɍ��݂̍s������BrainSetting�Ɣ��f�Ԋu�𔲂��Ă��ꂫ��
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BrainDataForJob : IDisposable
        {
            /// <summary>
            /// AI�̔��f�Ԋu
            /// </summary>
            [Header("���f�Ԋu")]
            public float judgeInterval;

            /// <summary>
            /// AI�̈ړ����f�Ԋu
            /// </summary>
            [Header("�ړ����f�Ԋu")]
            public float moveJudgeInterval;

            /// <summary>
            /// ActState��ϊ����� int ���L�[�Ƃ��čs���̃f�[�^������
            /// </summary>
            public NativeHashMap<int, BrainSettingForJob> brainSetting;

            /// <summary>
            /// �X�e�[�^�X�̃f�[�^����Job�p�̃f�[�^���\�z����B
            /// </summary>
            /// <param name="sourceDic"></param>
            /// <param name="judgeInterval"></param>
            public BrainDataForJob(ActStateBrainDictionary sourceDic, float judgeInterval, float moveJudgeInterval)
            {
                // ���f�Ԋu��ݒ�B
                this.judgeInterval = judgeInterval;
                this.moveJudgeInterval = moveJudgeInterval;

                brainSetting = new NativeHashMap<int, BrainSettingForJob>(sourceDic.Count, allocator: Allocator.Persistent);

                foreach ( var item in sourceDic )
                {
                    this.brainSetting.Add((int)item.Key, new BrainSettingForJob(item.Value, allocator: Allocator.Persistent));
                }
            }

            /// <summary>
            /// �C���^�[�o�����܂Ƃ߂ĕԂ��B
            /// </summary>
            /// <returns></returns>
            public float2 GetInterval()
            {
                return new float2(judgeInterval, moveJudgeInterval);
            }

            /// <summary>
            /// �e�L�����̐ݒ�Ƃ��ĕێ���������̂ŃQ�[���I�����ɌĂԁB
            /// </summary>
            public void Dispose()
            {
                foreach ( var item in brainSetting )
                {
                    item.Value.Dispose();
                }

                brainSetting.Dispose();
            }
        }

        /// <summary>
        /// AI�̐ݒ�B�iJob�V�X�e���d�l�j
        /// �X�e�[�^�X��CharacterSOAStatus����ڐA����B
        /// �v���C
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BrainSettingForJob : IDisposable
        {

            /// <summary>
            /// �s�������f�[�^
            /// </summary>
            [Header("�s�������f�[�^")]
            public NativeArray<BehaviorData> behaviorSetting;

            /// <summary>
            /// NativeArray���\�[�X���������
            /// </summary>
            public void Dispose()
            {
                if ( this.behaviorSetting.IsCreated )
                {
                    this.behaviorSetting.Dispose();
                }
            }

            /// <summary>
            /// �I���W�i����CharacterSOAStatus����f�[�^�𖾎��I�ɈڐA
            /// </summary>
            /// <param name="source">�ڐA���̃L�����N�^�[�u���C���X�e�[�^�X</param>
            /// <param name="allocator">NativeArray�Ɏg�p����A���P�[�^</param>
            public BrainSettingForJob(in BrainSetting source, Allocator allocator)
            {

                // �z���V�����쐬
                this.behaviorSetting = source.actCondition != null
                    ? new NativeArray<BehaviorData>(source.actCondition, allocator)
                    : new NativeArray<BehaviorData>(0, allocator);
            }

        }

        /// <summary>
        /// �w�C�g�̐ݒ�B�iJob�V�X�e���d�l�j
        /// �X�e�[�^�X����ڐA����B
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct HateSettingForJob
        {
            /// <summary>
            /// �U���ȊO�̍s�������f�[�^.
            /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
            /// </summary>
            [Header("�w�C�g�����f�[�^")]
            public NativeArray<TargetJudgeData> hateCondition;

            public HateSettingForJob(TargetJudgeData[] hateSetting)
            {
                hateCondition = new NativeArray<TargetJudgeData>(hateSetting, Allocator.Persistent);
            }
        }

        /// <summary>
        /// �s�����f���Ɏg�p����f�[�^�B
        /// �C���s�v:�����Ă��ꂼ���z��Ŏ��悤�ɂ����OK
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
        /// SoA OK
        /// 12Byte
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
        /// 56Byte
        /// SoA OK
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
        /// 52Byte 
        /// SoA OK
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
        /// 40Byte
        /// SoA OK
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
            public byte IsPassFilter(in SolidData solidData, in CharacterStateInfo stateInfo)
            {
                // and��or�œ�����������
                // ���Ă͂܂�Ȃ��Ȃ�A��B
                if ( this.isAndFeatureCheck == BitableBool.TRUE ? ((this.targetFeature != 0) && (this.targetFeature & solidData.feature) != this.targetFeature) :
                                                      ((this.targetFeature != 0) && (this.targetFeature & solidData.feature) == 0) )
                {
                    return 0;
                }

                // ������ʔ��f
                // ���Ă͂܂�Ȃ��Ȃ�A��B
                if ( this.isAndEffectCheck == BitableBool.TRUE ? ((this.targetEffect != 0) && (this.targetEffect & stateInfo.nowEffect) != this.targetEffect) :
                                          ((this.targetEffect != 0) && (this.targetEffect & stateInfo.nowEffect) == 0) )
                {
                    return 0;
                }

                // �C�x���g���f
                // ���Ă͂܂�Ȃ��Ȃ�A��B
                if ( this.isAndEventCheck == BitableBool.TRUE ? ((this.targetEvent != 0) && (this.targetEvent & stateInfo.brainEvent) != this.targetEvent) :
                                          ((this.targetEvent != 0) && (this.targetEvent & stateInfo.brainEvent) == 0) )
                {
                    return 0;
                }

                // �c��̏���������B
                if ( (this.targetType == 0 || ((this.targetType & stateInfo.belong) > 0)) && (this.targetState == 0 || ((this.targetState & stateInfo.actState) > 0))
                    && (this.targetWeakPoint == 0 || ((this.targetWeakPoint & solidData.weakPoint) > 0)) && (this.targetUseElement == 0 || ((this.targetUseElement & solidData.attackElement) > 0)) )
                {
                    return 1;
                }

                return 0;
            }

        }

        #endregion ���f�֘A

        #region �L�����N�^�[��f�[�^�i��Job�j

        /// <summary>
        /// ���M����f�[�^�A�s�ς̕�
        /// �唼�r�b�g�ł܂Ƃ߂ꂻ��
        /// ���ԋR�m�̓G���邩������Ȃ����^�C�v�͑g�ݍ��킹�\�ɂ���
        /// �������ȍ~�ł́A�X�e�[�^�X�o�t��f�o�t���؂ꂽ���Ɍ��ɖ߂����炢�����Ȃ�
        /// Job�V�X�e���Ŏg�p���Ȃ��̂Ń��������C�A�E�g�͍œK��
        /// SOA�ΏۊO
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
        /// �U�����[�V�����̃X�e�[�^�X�B
        /// ����̓_���[�W�v�Z�p�̃f�[�^������A�U�����ړ���G�t�F�N�g�̃f�[�^�͑��Ɏ��B
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

        #endregion

        #endregion SoA�ΏۊO

        #region �V���A���C�Y�\�ȃf�B�N�V���i���̒�`

        /// <summary>
        /// ActState���L�[��CharacterSOAStatus���l�̃f�B�N�V���i��
        /// </summary>
        [Serializable]
        public class ActStateBrainDictionary : SerializableDictionary<ActState, BrainSetting>
        {
        }

        #endregion

        // �������牺�Ŋe�f�[�^��ݒ�B�L�����̎�ނ��Ƃ̃X�e�[�^�X�B

        /// <summary>
        /// �L������ID
        /// </summary>
        public int characterID;

        /// <summary>
        /// AI�̔��f�Ԋu
        /// </summary>
        [Header("���f�Ԋu")]
        public float judgeInterval;

        /// <summary>
        /// AI�̈ړ����f�Ԋu
        /// </summary>
        [Header("�ړ����f�Ԋu")]
        public float moveJudgeInterval;

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
        /// �U���ȊO�̍s�������f�[�^.
        /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
        /// </summary>
        [Header("�w�C�g�����f�[�^")]
        public TargetJudgeData[] hateCondition;

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
        /// ���X�e�[�^�X�̃f�[�^���R�s�[����B
        /// �G�f�B�^�[�œ�������֐���p�ӂ��悤�B
        /// </summary>
        public BrainStatus source;

    }
}

