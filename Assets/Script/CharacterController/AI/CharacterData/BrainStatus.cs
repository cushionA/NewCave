using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.AIManager;

namespace CharacterController.StatusData
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
    public class BrainStatus : SerializedScriptableObject
    {
        #region Enum��`

        /// <summary>
        /// �������s�������肷�邽�߂̏���
        /// �����̎��A�U���E�񕜁E�x���E�����E��q�Ȃ�
        /// �Ώۂ������A�����A�G�̂ǂꂩ�Ƃ����敪�Ɣے�i�ȏオ�ȓ��ɂȂ�����j�t���O�̑g�ݍ��킹�ŕ\�� - > IsInvert�t���O
        /// ���������񂾂Ƃ��A�͎��S��ԂŐ��b�L�������c�����ƂŁA���S�������ɂ����t�B���^�[�ɂ�����悤�ɂ��邩�B
        /// </summary>
        public enum ActJudgeCondition : byte
        {
            �w��̃w�C�g�l�̓G�����鎞 = 1,
            //�Ώۂ���萔�̎� = 2, // �t�B���^�[�����p���邱�ƂŁA�����ł��Ȃ�̐��̒P���ȏ����͂���B��̈ȏ�����Ń^�C�v�t�B���^�[�őΏۂ̃^�C�v�i������
            HP����芄���̑Ώۂ����鎞 = 2,
            MP����芄���̑Ώۂ����鎞 = 3,
            �ݒ苗���ɑΏۂ����鎞 = 4,  //�����n�̏����͕ʂ̂����Ŏ��O�ɃL���b�V�����s���BAI�̐ݒ�͈̔͂����Z���T�[�Œ��ׂ���@���Ƃ�B���f���ɂ��悤�ɂ���H
            ����̑����ōU������Ώۂ����鎞 = 5,
            ����̐��̓G�ɑ_���Ă��鎞 = 6,// �w�c�t�B���^�����O�͗L��
            �ߋ����ɑΏۂ���萔���鎞,// ������
            �����Ȃ� = 0 // �������Ă͂܂�Ȃ��������̕⌇�����B
        }

        /// <summary>
        /// �N�[���^�C�����L�����Z���������
        /// �͈͂Őݒ肷��悤�ɂ��悤
        /// </summary>
        public enum SkipJudgeCondition : byte
        {
            ������HP����芄���̎� = 1,
            ������MP����芄���̎�,
            ������HP����芄���̎�,
            ������MP����芄���̎�,
            �G��HP����芄���̎�,
            �G��MP����芄���̎�,
            �����̋��������̎�,
            �G�̋��������̎�,
            �����Ȃ� = 0 // �N�[���^�C���X�L�b�v�Ȃ�
        }

        /// <summary>
        /// ���f�̌��ʑI�������s���̃^�C�v�B
        /// ����͍s�����N��������ɍ��������Ă���A�Ƃ��������Ŏg���H
        /// </summary>
        [Flags]
        public enum ActState
        {
            �w��Ȃ� = 0,// �X�e�[�g�t�B���^�[���f�Ŏg���B�����w�肵�Ȃ��B
            �ǐ� = 1 << 0,
            ���� = 1 << 1,
            �U�� = 1 << 2,
            �ҋ@ = 1 << 3,// �U����̃N�[���^�C�����ȂǁB���̏�Ԃœ��삷���𗦂�ݒ肷��H �ړ�������
            �h�� = 1 << 4,// �����o��������ݒ�ł���悤�ɂ���H ���̏�Ŋ�{�K�[�h�����ǁA���肪�����炩���ꂽ�瓮���o���A�I��
            �x�� = 1 << 5,
            �� = 1 << 6,
            �W�� = 1 << 7,// ����̖����̏ꏊ�ɍs���B�W����ɖh��Ɉڍs���郍�W�b�N��g�߂Ό�q�ɂȂ�Ȃ��H
            �g���K�[�s�� = 1 << 8 // �g���K�[�ɉ����ē���̍s�����s�����߂̍s������B
        }

        /// <summary>
        /// �G�ɑ΂���w�C�g�l�̏㏸�A�����̏����B
        /// �����ɓ��Ă͂܂�G�̃w�C�g�l���㏸�����茸�������肷��B
        /// ���邢�͖����̎x���E�񕜁E��q�Ώۂ����߂�
        /// ������ے�t���O�Ƃ̑g�ݍ��킹�Ŏg��
        /// </summary>
        public enum TargetSelectCondition : byte
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
        public enum CharacterBelong : byte
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
        public enum Element : byte
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
        public enum CharacterRank : byte
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
        public enum SpecialEffect : byte
        {
            �w�C�g���� = 1 << 1,
            �w�C�g���� = 1 << 2,
            �Ȃ� = 0,
        }

        /// <summary>
        /// �F�����Ă���I�u�W�F�N�g�������r�b�g�t���O
        /// </summary>
        [Flags]
        public enum RecognizeObjectType
        {
            �����Ȃ� = 0,
            �A�C�e�� = 1 << 0, // �A�C�e����F��
            �v���C���[���L���� = 1 << 1, // �v���C���[���̃L������F��
            �������L���� = 1 << 2, // �G���̃L������F��
            �������L���� = 1 << 3, // �������̃L������F��
            �댯�� = 1 << 4, // �댯����F��
            ��ѓ���U�� = 1 << 5, // �U����F��
            �o�t�G���A = 1 << 6, // �o�t�G���A��F��
            �f�o�t�G���A = 1 << 7, // �f�o�t�G���A��F��
            ���� = 1 << 8, // �����F��
            �ŏ� = 1 << 9, // �ŏ���F��
            �_���[�W�G���A = 1 << 10, // �_���[�W�G���A��F��
            �j��\�I�u�W�F�N�g = 1 << 11, // �j��\�ȃI�u�W�F�N�g��F��
            �悶�o��|�C���g = 1 << 12, // �R��F��
        }

        /// <summary>
        /// bitable�Ȑ^�U�l
        /// Job�V�X�e���A�Ƃ������l�C�e�B�u�R�[�h�� bool �̑������ǂ��Ȃ����ߎ���
        /// </summary>
        public enum BitableBool : byte
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
        /// �Z���T�[��ʂ��Ċl���������͂̔F���f�[�^�B
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct RecognitionData
        {
            /// <summary>
            /// ���ߋ����̃v���C���[�w�c�L�����̐�
            /// </summary>
            public byte nearlyPlayerSideCount;

            /// <summary>
            /// ���ߋ����̓G�L�����w�c�̃L�����̐�
            /// </summary>
            public byte nearlyMonsterSideCount;

            /// <summary>
            /// ���ߋ����̒����w�c�L�����̐�
            /// </summary>
            public byte nearlyOtherSideCount;

            /// <summary>
            /// ���ݔF�����Ă���I�u�W�F�N�g���
            /// </summary>
            public RecognizeObjectType recognizeObject;

            public void Reset()
            {
                this.nearlyPlayerSideCount = 0;
                this.nearlyMonsterSideCount = 0;
                this.nearlyOtherSideCount = 0;
                this.recognizeObject = RecognizeObjectType.�����Ȃ�;
            }
        }

        /// <summary>
        /// �L�����̍s���i���s���x�Ƃ��j�̃X�e�[�^�X�B
        /// �ړ����x�ȂǁB
        /// ���Əc�ǂ���̋�����D��ŋl�߂邩�Ƃ��W�����v�֘A�̂�����ꂽ��������������
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
                this.hpRatio = this.maxHp > 0 ? this.currentHp * 100 / this.maxHp : 0;
                this.mpRatio = this.maxMp > 0 ? this.currentMp * 100 / this.maxMp : 0;
            }

            /// <summary>
            /// CharacterBaseData�����{����ݒ�
            /// </summary>
            public CharacterBaseInfo(in CharacterBaseData baseData, Vector2 initialPosition)
            {
                this.maxHp = baseData.hp;
                this.maxMp = baseData.mp;
                this.currentHp = baseData.hp;
                this.currentMp = baseData.mp;
                this.hpRatio = 100;
                this.mpRatio = 100;
                this.nowPosition = initialPosition;
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
        /// 32byte
        /// </summary>
        public struct CharacterColdLog
        {
            /// <summary>
            /// �L�����N�^�[�̃}�X�^�|�f�[�^���ID
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

            /// <summary>
            /// ���݂̃N�[���^�C���̃f�[�^�B
            /// </summary>
            [HideInInspector]
            public CoolTimeData nowCoolTime;

            public CharacterColdLog(BrainStatus status, int hash)
            {
                this.characterID = status.characterID;
                this.hashCode = hash;
                // �ŏ��̓}�C�i�X��10000�����邱�Ƃł���������悤��
                this.lastJudgeTime = -10000;
                this.lastMoveJudgeTime = -10000;
                this.nowCoolTime = new CoolTimeData();
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
                this.dispAtk = this.atk.ReturnSum();
            }

            /// <summary>
            /// CharacterBaseData����퓬�X�e�[�^�X��ݒ�
            /// </summary>
            public CharacterAtkStatus(in CharacterBaseData baseData)
            {
                this.atk = baseData.baseAtk;
                this.dispAtk = this.atk.ReturnSum();
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
                this.dispDef = this.def.ReturnSum();
            }

            /// <summary>
            /// CharacterBaseData����퓬�X�e�[�^�X��ݒ�
            /// </summary>
            public CharacterDefStatus(in CharacterBaseData baseData)
            {
                this.def = baseData.baseDef;
                this.dispDef = this.def.ReturnSum();
            }
        }

        /// <summary>
        /// StateImfo region - �L�����N�^�[�̏�ԏ��
        /// �T�C�Y: 16�o�C�g�i1�L���b�V�����C����25%�j
        /// �p�r: AI���f�A��ԊǗ�
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterStateInfo
        {
            /// <summary>
            /// ���݂̃L�����N�^�[�̏���
            /// </summary>
            public CharacterBelong belong;

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
                this.nowEffect = SpecialEffect.�Ȃ�;
                this.brainEvent = BrainEventFlagType.None;
            }

            /// <summary>
            /// CharacterBaseData�����ԏ���ݒ�
            /// </summary>
            public CharacterStateInfo(in CharacterBaseData baseData)
            {
                this.belong = baseData.initialBelong;
                this.actState = baseData.initialMove;
                this.nowEffect = SpecialEffect.�Ȃ�;
                this.brainEvent = BrainEventFlagType.None;
                this.targetingCount = 0;
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
            public BehaviorData[] judgeData;

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

                this.brainSetting = new NativeHashMap<int, BrainSettingForJob>(sourceDic.Count, allocator: Allocator.Persistent);

                foreach ( KeyValuePair<ActState, BrainSetting> item in sourceDic )
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
                return new float2(this.judgeInterval, this.moveJudgeInterval);
            }

            /// <summary>
            /// �e�L�����̐ݒ�Ƃ��ĕێ���������̂ŃQ�[���I�����ɌĂԁB
            /// </summary>
            public void Dispose()
            {
                foreach ( KVPair<int, BrainSettingForJob> item in this.brainSetting )
                {
                    item.Value.Dispose();
                }

                this.brainSetting.Dispose();
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
                this.behaviorSetting = source.judgeData != null
                    ? new NativeArray<BehaviorData>(source.judgeData, allocator)
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
                this.hateCondition = new NativeArray<TargetJudgeData>(hateSetting, Allocator.Persistent);
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
            public CoolTimeData coolTimeData;

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
        /// �s����̃N�[���^�C���L�����Z�����f�Ɏg�p����f�[�^�B
        /// SoA OK
        /// 10Byte
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CoolTimeData
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

            /// <summary>
            /// �ݒ肷��N�[���^�C���B
            /// </summary>
            public float coolTime;
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
        /// 22Byte
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct TargetFilter : IEquatable<TargetFilter>
        {
            /// <summary>
            /// �e�t�B���^�[������And���f�A�܂�w�肵���S���������Ă͂܂邩�ǂ����𔻒f���邩���r�b�g�t���O�Ŏ����߂̗񋓌^�B
            /// </summary>
            [Flags]
            public enum FilterBitFlag : byte
            {
                �����t�B���^�[_And���f = 1 << 0,
                ������ʃt�B���^�[_And���f = 1 << 1,
                �s����ԃt�B���^�[_And���f = 1 << 2,
                �C�x���g�t�B���^�[_And���f = 1 << 3,
                ��_�����t�B���^�[_And���f = 1 << 4,
                �g�p�����t�B���^�[_And���f = 1 << 5
            }

            /// <summary>
            /// �e�t�B���^�[������AND/OR������Ǘ�����r�b�g�t���O
            /// </summary>
            [Header("�t�B���^�[������@")]
            [SerializeField]
            private FilterBitFlag _filterFlags;

            /// <summary>
            /// �Ώۂ̐w�c�敪
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ̐w�c")]
            [SerializeField]
            private CharacterBelong _targetType;

            /// <summary>
            /// �Ώۂ̓���
            /// �����w�肠��
            /// intEnum
            /// </summary>
            [Header("�Ώۂ̓���")]
            [SerializeField]
            private CharacterFeature _targetFeature;

            /// <summary>
            /// �Ώۂ̏�ԁi�o�t�A�f�o�t�j
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ����������")]
            [SerializeField]
            private SpecialEffect _targetEffect;

            /// <summary>
            /// �Ώۂ̏�ԁi�����A�U���Ȃǁj
            /// �����w�肠��
            /// intEnum
            /// </summary>
            [Header("�Ώۂ̏��")]
            [SerializeField]
            private ActState _targetState;

            /// <summary>
            /// �Ώۂ̃C�x���g�󋵁i��_���[�W��^�����A�Ƃ��j�Ńt�B���^�����O
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ̃C�x���g")]
            [SerializeField]
            private BrainEventFlagType _targetEvent;

            /// <summary>
            /// �Ώۂ̎�_�����Ńt�B���^�����O
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ̎�_")]
            [SerializeField]
            private Element _targetWeakPoint;

            /// <summary>
            /// �Ώۂ��g�������Ńt�B���^�����O
            /// �����w�肠��
            /// </summary>
            [Header("�Ώۂ̎g�p����")]
            [SerializeField]
            private Element _targetUseElement;

            /// <summary>
            /// �Ώۂ̋����Ńt�B���^�����O
            /// </summary>
            [Header("�Ώۂ̋����͈�")]
            [SerializeField]
            private float2 _distanceRange;

            /// <summary>
            /// �����ΏۃL�����N�^�[�̏����ɓ��Ă͂܂邩���`�F�b�N����B
            /// </summary>
            /// <param name="solidData"></param>
            /// <param name="stateInfo"></param>
            /// <returns></returns>
            [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
            public byte IsPassFilter(in SolidData solidData, in CharacterStateInfo stateInfo, float2 nowPosition, float2 targetPosition)
            {
                // ���ׂĂ̏�����2��uint4�Ƀp�b�N
                uint4 masks1 = new(
                    (uint)this._targetFeature,
                    (uint)this._targetEffect,
                    (uint)this._targetEvent,
                    (uint)this._targetType
                );

                uint4 values1 = new(
                    (uint)solidData.feature,
                    (uint)stateInfo.nowEffect,
                    (uint)stateInfo.brainEvent,
                    (uint)stateInfo.belong
                );

                uint4 masks2 = new(
                    (uint)this._targetState,
                    (uint)this._targetWeakPoint,
                    (uint)this._targetUseElement,
                    0u
                );

                uint4 values2 = new(
                    (uint)stateInfo.actState,
                    (uint)solidData.weakPoint,
                    (uint)solidData.attackElement,
                    0u
                );

                // FilterBitFlag����AND/OR����^�C�v���擾
                bool4 checkTypes1 = new(
                    (this._filterFlags & FilterBitFlag.�����t�B���^�[_And���f) != 0,
                    (this._filterFlags & FilterBitFlag.������ʃt�B���^�[_And���f) != 0,
                    (this._filterFlags & FilterBitFlag.�C�x���g�t�B���^�[_And���f) != 0,
                    false // targetType�͏��OR����
                );

                bool4 checkTypes2 = new(
                    (this._filterFlags & FilterBitFlag.�s����ԃt�B���^�[_And���f) != 0,
                    (this._filterFlags & FilterBitFlag.��_�����t�B���^�[_And���f) != 0,
                    (this._filterFlags & FilterBitFlag.�g�p�����t�B���^�[_And���f) != 0,
                    false
                );

                // SIMD���Z
                uint4 and1 = masks1 & values1;
                uint4 and2 = masks2 & values2;

                // ��������
                bool4 pass1 = EvaluateConditions(masks1, and1, checkTypes1);
                bool4 pass2 = EvaluateConditions(masks2, and2, checkTypes2);

                // ���ׂĂ̏�������������Ă��邩���`�F�b�N
                if ( math.all(pass1) && math.all(pass2) )
                {
                    // ��������������Ă���ꍇ�A�����`�F�b�N���s��
                    if ( math.any(this._distanceRange != float2.zero) )
                    {
                        // �����`�F�b�N
                        float distance = math.distancesq(nowPosition, targetPosition);
                        bool2 distanceCheck = new bool2(
                            this._distanceRange.x == 0 || distance >= math.pow(this._distanceRange.x, 2),
                            this._distanceRange.y == 0 || distance <= math.pow(this._distanceRange.y, 2)
                        );

                        // ����������AND�Ō���
                        return math.all(distanceCheck) ? (byte)1 : (byte)0;
                    }

                    // �����`�F�b�N�s�v�ł��ׂĂ̏�������������Ă���ꍇ��1��Ԃ�
                    return 1;
                }

                // ��������������Ă��Ȃ��ꍇ��0��Ԃ�
                return (byte)0;
            }

            /// <summary>
            /// �����]����SIMD�Ŏ��s����w���p�[���\�b�h
            /// </summary>
            [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
            private static bool4 EvaluateConditions(uint4 masks, uint4 andResults, bool4 isAndCheck)
            {
                bool4 zeroMasks = masks == 0u;
                bool4 andConditions = andResults == masks;
                bool4 orConditions = andResults > 0u;

                // �r�b�g���Z�ŏ����I��������
                // isAndCheck �� true �̏ꍇ�� andConditions�Afalse �̏ꍇ�� orConditions
                bool4 selectedConditions = (isAndCheck & andConditions) | (!isAndCheck & orConditions);

                return zeroMasks | selectedConditions;
            }

            #region �f�o�b�O�p

            public CharacterBelong GetTargetType()
            {
                return this._targetType;
            }

            public bool Equals(TargetFilter other)
            {
                return this._targetType == other._targetType &&
                       this._targetFeature == other._targetFeature &&
                       this._targetEffect == other._targetEffect &&
                       this._targetState == other._targetState &&
                       this._targetEvent == other._targetEvent &&
                       this._targetWeakPoint == other._targetWeakPoint &&
                       this._targetUseElement == other._targetUseElement &&
                       this._filterFlags == other._filterFlags;
            }

            /// <summary>
            /// �f�o�b�O�p�̃f�R���X�g���N�^�B
            /// var (type, feature, effect, state, eventType, weakPoint, useElement, filterFlags) = filter;
            /// </summary>
            public void Deconstruct(
                out CharacterBelong targetType,
                out CharacterFeature targetFeature,
                out SpecialEffect targetEffect,
                out ActState targetState,
                out BrainEventFlagType targetEvent,
                out Element targetWeakPoint,
                out Element targetUseElement,
                out FilterBitFlag filterFlags)
            {
                targetType = this._targetType;
                targetFeature = this._targetFeature;
                targetEffect = this._targetEffect;
                targetState = this._targetState;
                targetEvent = this._targetEvent;
                targetWeakPoint = this._targetWeakPoint;
                targetUseElement = this._targetUseElement;
                filterFlags = this._filterFlags;
            }

            /// <summary>
            /// �݊����̂��߂̃f�R���X�g���N�^�iBitableBool�`���j
            /// </summary>
            public void Deconstruct(
                out CharacterBelong targetType,
                out CharacterFeature targetFeature,
                out bool isAndFeatureCheck,
                out SpecialEffect targetEffect,
                out bool isAndEffectCheck,
                out ActState targetState,
                out BrainEventFlagType targetEvent,
                out bool isAndEventCheck,
                out Element targetWeakPoint,
                out Element targetUseElement)
            {
                targetType = this._targetType;
                targetFeature = this._targetFeature;
                isAndFeatureCheck = (this._filterFlags & FilterBitFlag.�����t�B���^�[_And���f) != 0;
                targetEffect = this._targetEffect;
                isAndEffectCheck = (this._filterFlags & FilterBitFlag.������ʃt�B���^�[_And���f) != 0;
                targetState = this._targetState;
                targetEvent = this._targetEvent;
                isAndEventCheck = (this._filterFlags & FilterBitFlag.�C�x���g�t�B���^�[_And���f) != 0;
                targetWeakPoint = this._targetWeakPoint;
                targetUseElement = this._targetUseElement;
            }

            /// <summary>
            /// IsPassFilter�̃f�o�b�O�p���\�b�h�B���s���������̏ڍׂ�Ԃ�
            /// </summary>
            public string DebugIsPassFilter(in SolidData solidData, in CharacterStateInfo stateInfo)
            {
                System.Text.StringBuilder failedConditions = new();

                // 1. ������������
                if ( this._targetFeature != 0 )
                {
                    bool featureFailed = false;
                    string failureReason = "";
                    bool isAndCheck = (this._filterFlags & FilterBitFlag.�����t�B���^�[_And���f) != 0;

                    if ( isAndCheck )
                    {
                        // AND�����F�S�Ă̓������K�v
                        if ( (this._targetFeature & solidData.feature) != this._targetFeature )
                        {
                            featureFailed = true;
                            CharacterFeature missingFeatures = this._targetFeature & ~solidData.feature;
                            failureReason = $"AND�������s - �K�v�ȓ������s��: {missingFeatures}";
                        }
                    }
                    else
                    {
                        // OR�����F�����ꂩ�̓������K�v
                        if ( (this._targetFeature & solidData.feature) == 0 )
                        {
                            featureFailed = true;
                            failureReason = "OR�������s - ��v��������Ȃ�";
                        }
                    }

                    if ( featureFailed )
                    {
                        _ = failedConditions.AppendLine($"[���������Ŏ��s]");
                        _ = failedConditions.AppendLine($"  �t�B�[���h: targetFeature");
                        _ = failedConditions.AppendLine($"  ���Ғl: {this._targetFeature} (0x{this._targetFeature:X})");
                        _ = failedConditions.AppendLine($"  ���ۂ̒l: {solidData.feature} (0x{solidData.feature:X})");
                        _ = failedConditions.AppendLine($"  ������@: {(isAndCheck ? "AND" : "OR")}");
                        _ = failedConditions.AppendLine($"  ���R: {failureReason}");
                        _ = failedConditions.AppendLine();
                        return failedConditions.ToString();
                    }
                }

                // 2. ������ʔ��f
                if ( this._targetEffect != 0 )
                {
                    bool effectFailed = false;
                    string failureReason = "";
                    bool isAndCheck = (this._filterFlags & FilterBitFlag.������ʃt�B���^�[_And���f) != 0;

                    if ( isAndCheck )
                    {
                        // AND�����F�S�Ă̌��ʂ��K�v
                        if ( (this._targetEffect & stateInfo.nowEffect) != this._targetEffect )
                        {
                            effectFailed = true;
                            SpecialEffect missingEffects = this._targetEffect & ~stateInfo.nowEffect;
                            failureReason = $"AND�������s - �K�v�Ȍ��ʂ��s��: {missingEffects}";
                        }
                    }
                    else
                    {
                        // OR�����F�����ꂩ�̌��ʂ��K�v
                        if ( (this._targetEffect & stateInfo.nowEffect) == 0 )
                        {
                            effectFailed = true;
                            failureReason = "OR�������s - ��v������ʂȂ�";
                        }
                    }

                    if ( effectFailed )
                    {
                        _ = failedConditions.AppendLine($"[������ʏ����Ŏ��s]");
                        _ = failedConditions.AppendLine($"  �t�B�[���h: targetEffect");
                        _ = failedConditions.AppendLine($"  ���Ғl: {this._targetEffect} (0x{this._targetEffect:X})");
                        _ = failedConditions.AppendLine($"  ���ۂ̒l: {stateInfo.nowEffect} (0x{stateInfo.nowEffect:X})");
                        _ = failedConditions.AppendLine($"  ������@: {(isAndCheck ? "AND" : "OR")}");
                        _ = failedConditions.AppendLine($"  ���R: {failureReason}");
                        _ = failedConditions.AppendLine();
                        return failedConditions.ToString();
                    }
                }

                // 3. �C�x���g���f
                if ( this._targetEvent != 0 )
                {
                    bool eventFailed = false;
                    string failureReason = "";
                    bool isAndCheck = (this._filterFlags & FilterBitFlag.�C�x���g�t�B���^�[_And���f) != 0;

                    if ( isAndCheck )
                    {
                        // AND�����F�S�ẴC�x���g���K�v
                        if ( (this._targetEvent & stateInfo.brainEvent) != this._targetEvent )
                        {
                            eventFailed = true;
                            BrainEventFlagType missingEvents = this._targetEvent & ~stateInfo.brainEvent;
                            failureReason = $"AND�������s - �K�v�ȃC�x���g���s��: {missingEvents}";
                        }
                    }
                    else
                    {
                        // OR�����F�����ꂩ�̃C�x���g���K�v
                        if ( (this._targetEvent & stateInfo.brainEvent) == 0 )
                        {
                            eventFailed = true;
                            failureReason = "OR�������s - ��v����C�x���g�Ȃ�";
                        }
                    }

                    if ( eventFailed )
                    {
                        _ = failedConditions.AppendLine($"[�C�x���g�����Ŏ��s]");
                        _ = failedConditions.AppendLine($"  �t�B�[���h: targetEvent");
                        _ = failedConditions.AppendLine($"  ���Ғl: {this._targetEvent} (0x{this._targetEvent:X})");
                        _ = failedConditions.AppendLine($"  ���ۂ̒l: {stateInfo.brainEvent} (0x{stateInfo.brainEvent:X})");
                        _ = failedConditions.AppendLine($"  ������@: {(isAndCheck ? "AND" : "OR")}");
                        _ = failedConditions.AppendLine($"  ���R: {failureReason}");
                        _ = failedConditions.AppendLine();
                        return failedConditions.ToString();
                    }
                }

                // 4. �c��̏����i�ʃ`�F�b�N�j
                List<string> remainingFailures = new();

                // �w�c�`�F�b�N
                if ( this._targetType != 0 && (this._targetType & stateInfo.belong) == 0 )
                {
                    remainingFailures.Add($"  - targetType: ���Ғl={this._targetType} (0x{this._targetType:X}), ���ۂ̒l={stateInfo.belong} (0x{stateInfo.belong:X})");
                }

                // ��ԃ`�F�b�N�iAND/OR����Ή��j
                if ( this._targetState != 0 )
                {
                    bool isAndCheck = (this._filterFlags & FilterBitFlag.�s����ԃt�B���^�[_And���f) != 0;
                    bool statePassed = isAndCheck ?
                        (this._targetState & stateInfo.actState) == this._targetState :
                        (this._targetState & stateInfo.actState) != 0;

                    if ( !statePassed )
                    {
                        remainingFailures.Add($"  - targetState: ���Ғl={this._targetState} (0x{this._targetState:X}), ���ۂ̒l={stateInfo.actState} (0x{stateInfo.actState:X}), ����={(isAndCheck ? "AND" : "OR")}");
                    }
                }

                // ��_�`�F�b�N�iAND/OR����Ή��j
                if ( this._targetWeakPoint != 0 )
                {
                    bool isAndCheck = (this._filterFlags & FilterBitFlag.��_�����t�B���^�[_And���f) != 0;
                    bool weakPointPassed = isAndCheck ?
                        (this._targetWeakPoint & solidData.weakPoint) == this._targetWeakPoint :
                        (this._targetWeakPoint & solidData.weakPoint) != 0;

                    if ( !weakPointPassed )
                    {
                        remainingFailures.Add($"  - targetWeakPoint: ���Ғl={this._targetWeakPoint} (0x{this._targetWeakPoint:X}), ���ۂ̒l={solidData.weakPoint} (0x{solidData.weakPoint:X}), ����={(isAndCheck ? "AND" : "OR")}");
                    }
                }

                // �g�p�����`�F�b�N�iAND/OR����Ή��j
                if ( this._targetUseElement != 0 )
                {
                    bool isAndCheck = (this._filterFlags & FilterBitFlag.�g�p�����t�B���^�[_And���f) != 0;
                    bool useElementPassed = isAndCheck ?
                        (this._targetUseElement & solidData.attackElement) == this._targetUseElement :
                        (this._targetUseElement & solidData.attackElement) != 0;

                    if ( !useElementPassed )
                    {
                        remainingFailures.Add($"  - targetUseElement: ���Ғl={this._targetUseElement} (0x{this._targetUseElement:X}), ���ۂ̒l={solidData.attackElement} (0x{solidData.attackElement:X}), ����={(isAndCheck ? "AND" : "OR")}");
                    }
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
            /// ���ڍׂȃr�b�g��͂��܂ރo�[�W����
            /// </summary>
            /// <param name="solidData"></param>
            /// <param name="stateInfo"></param>
            /// <returns></returns>
            public string DebugIsPassFilterDetailed(in SolidData solidData, in CharacterStateInfo stateInfo)
            {
                string result = this.DebugIsPassFilter(solidData, stateInfo);

                if ( result != "�S�Ă̏������p�X���܂���" )
                {
                    System.Text.StringBuilder details = new();
                    _ = details.AppendLine("=== �ڍׂȃr�b�g��� ===");
                    _ = details.AppendLine($"FilterFlags: {this._filterFlags} (0x{(int)this._filterFlags:X})");

                    // �t�B���^�[�t���O�̏ڍ�
                    _ = details.AppendLine("�t�B���^�[�ݒ�:");
                    _ = details.AppendLine($"  �����t�B���^�[: {((this._filterFlags & FilterBitFlag.�����t�B���^�[_And���f) != 0 ? "AND" : "OR")}");
                    _ = details.AppendLine($"  ������ʃt�B���^�[: {((this._filterFlags & FilterBitFlag.������ʃt�B���^�[_And���f) != 0 ? "AND" : "OR")}");
                    _ = details.AppendLine($"  �s����ԃt�B���^�[: {((this._filterFlags & FilterBitFlag.�s����ԃt�B���^�[_And���f) != 0 ? "AND" : "OR")}");
                    _ = details.AppendLine($"  �C�x���g�t�B���^�[: {((this._filterFlags & FilterBitFlag.�C�x���g�t�B���^�[_And���f) != 0 ? "AND" : "OR")}");
                    _ = details.AppendLine($"  ��_�����t�B���^�[: {((this._filterFlags & FilterBitFlag.��_�����t�B���^�[_And���f) != 0 ? "AND" : "OR")}");
                    _ = details.AppendLine($"  �g�p�����t�B���^�[: {((this._filterFlags & FilterBitFlag.�g�p�����t�B���^�[_And���f) != 0 ? "AND" : "OR")}");

                    result += "\n" + details.ToString();
                }

                return result;
            }
            #endregion
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
            public CharacterBelong initialBelong;
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

        #region �������p�̃f�R���X�g���N�^

        /// <summary>
        /// �f�R���X�g���N�^�ɂ�肷�ׂẴf�[�^���X�g���^�v���Ƃ��ĕԂ�
        /// coldData�ɂ��Ă͂��ƂŃI�u�W�F�N�g������
        /// </summary>
        public void Deconstruct(
            out CharacterBaseInfo characterBaseInfo,
            out CharacterAtkStatus characterAtkStatus,
            out CharacterDefStatus characterDefStatus,
            out SolidData solidData,
            out CharacterStateInfo characterStateInfo,
            out MoveStatus moveStatus)
        {
            characterBaseInfo = new CharacterBaseInfo(this.baseData, Vector2.zero);
            characterAtkStatus = new CharacterAtkStatus(this.baseData);
            characterDefStatus = new CharacterDefStatus(this.baseData);
            solidData = this.solidData;
            characterStateInfo = new CharacterStateInfo(this.baseData);
            moveStatus = this.moveStatus;
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