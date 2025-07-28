using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static CharacterController.AIManager;
using static CharacterController.StatusData.BrainStatus.TriggerJudgeData;

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
        /// �������^�[�Q�b�g���Đݒ�⃂�[�h�ύX��������B
        /// �܂��A�N�[���^�C�����������ɂ��g�p����B
        /// �����̎��A�U���E�񕜁E�x���E�����E��q�Ȃ�
        /// �Ώۂ������A�����A�G�̂ǂꂩ�Ƃ����敪�Ɣے�i�ȏオ�ȓ��ɂȂ�����j�t���O�̑g�ݍ��킹�ŕ\�� - > IsInvert�t���O
        /// ���������񂾂Ƃ��A�͎��S��ԂŐ��b�L�������c�����ƂŁA���S�������ɂ����t�B���^�[�ɂ�����悤�ɂ���B
        /// 
        /// �e�����ɗD��x�����邱�ƂŁA���݂̃^�[�Q�b�g���G�Œ�����Ԃ��A�Ƃ����߂Ŏw�肳�ꂽ���肩�A�Ƃ��̔���𒴂�����悤�ɂ���
        /// </summary>
        public enum ActTriggerCondition : byte
        {
            ����̑Ώۂ���萔���鎞 = 1, //    �t�B���^�[���g���B���������n�̈ȏ�A���Č`�ɂ���B
            HP����芄���̑Ώۂ����鎞,
            MP����芄���̑Ώۂ����鎞,
            �Ώۂ̃L�����̎��͂ɓ���w�c�����ȏ㖧�W���Ă��鎞, // �F���f�[�^���炻���̓G�����̋ߋ��������g��
            �Ώۂ̃L�����̎��͂ɓ���w�c�����ȉ��������Ȃ���,
            ���͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞, // �F���f�[�^����I�u�W�F�N�g�̎�ނ��g��
            �Ώۂ���萔�̓G�ɑ_���Ă��鎞,// �w�c�t�B���^�����O
            �Ώۂ̃L�����̈�苗���ȓ��ɔ�ѓ�����鎞, // �F���f�[�^����ߋ����T�m�̕��̔�ѓ���̌��m���g���B��u���������S�����鏂�Ƃ��p�ӂ��邩
            ����̃C�x���g������������, // �C�x���g�V�X�e���Ŕ��������C�x���g���m�F����B�m�F����܂ł͋N�����C�x���g�͏����Ȃ��B
                           // ����l�ɂ͐w�c�ƃC�x���g���Z�b�g����B
            �����Ȃ� = 0 // �������Ă͂܂�Ȃ��������̕⌇�����B
        }

        /// <summary>
        /// �^�[�Q�b�g��I������ۂ̏���
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
            �V�X�^�[����,
            �v���C���[�w�c�̖��W�l��,
            �����w�c�̖��W�l��,
            ���̑��w�c�̖��W�l��,
            �����𖞂����ΏۂɂƂ��čł��w�C�g�������L����,
            �����𖞂����ΏۂɍŌ�ɍU�������L����,
            �w��Ȃ�_�t�B���^�[�̂�, // ��{�̏����B�Ώۂ̒��Ńt�B���^�[�ɓ��Ă͂܂��������I��
        }

        /// <summary>
        /// �^�[�Q�b�g���Œ肳�ꂽ��Ԃōs����I�Ԃ��߂̏����B
        /// �����̎��A�U���E�񕜁E�x���E�����E��q�Ȃ�
        /// 
        /// ���̏����ł��^�[�Q�b�g�ύX�Ƃ��g���K�[�ł���悤�ɂ���H
        /// </summary>
        public enum MoveSelectCondition : byte
        {
            // �Ώۂ͎������^�[�Q�b�g����I�ׂ�B�t���O�ł�
            �Ώۂ��t�B���^�[�ɓ��Ă͂܂鎞 = 1, // �t�B���^�[���g���B���̏����ł��t�B���^�[�͎g����
            �Ώۂ�HP����芄���̎�,
            �Ώۂ�MP����芄���̎�,
            �Ώۂ̎��͂ɓ���w�c�̃L���������ȏ㖧�W���Ă��鎞, // �F���f�[�^���炻���̓G�����̋ߋ��������g��
            �Ώۂ̎��͂ɓ���w�c�̃L���������ȉ��������Ȃ���, // �F���f�[�^���炻���̓G�����̋ߋ��������g��
            �Ώۂ̎��͂Ɏw��̃I�u�W�F�N�g��n�`�����鎞, // �F���f�[�^����I�u�W�F�N�g�̎�ނ��g��
            �Ώۂ�����̐��̓G�ɑ_���Ă��鎞,// �w�c�t�B���^�����O
            �Ώۂ̈�苗���ȓ��ɔ�ѓ�����鎞, // �F���f�[�^����ߋ����T�m�̕��̔�ѓ���̌��m���g���B��u���������S�����鏂�Ƃ��p�ӂ��邩
            ����̃C�x���g������������, // �C�x���g�V�X�e���Ŕ��������C�x���g���m�F����B�m�F����܂ł͋N�����C�x���g�͏����Ȃ��B
                           // �� ��l�ɂ͐w�c�ƃC�x���g���Z�b�g����B�ΏۂƂ͊֌W�Ȃ����f�����������͓���邩
            ///���[�h�`�F���W�������̕b�����o�߂�����,// ���[�h�`�F���W��̍ŏ��̍s����An�b�o�ߌ�̍s���𐧌�B
            // �R���g���[���[�Ń��[�h�n�̃f�[�^���L�^���ăC�x���g�o���̂ŃC�x���g�ɓ���
            �^�[�Q�b�g�������̏ꍇ,
            �����Ȃ� = 0 // �������Ă͂܂�Ȃ��������̕⌇�����B
        }

        /// <summary>
        /// ���f�̌��ʑI�������s���̃^�C�v�B
        /// ����͍s�����N��������ɍ��������Ă���A�Ƃ��������Ŏg���H
        /// </summary>
        [Flags]
        public enum ActState : byte
        {
            �w��Ȃ� = 0,// �X�e�[�g�t�B���^�[���f�Ŏg���B�����w�肵�Ȃ��B
            ���� = 1 << 0,
            �U�� = 1 << 1,
            �ړ� = 1 << 2,// �U����̃N�[���^�C�����ȂǁB���̏�Ԃœ��삷���𗦂�ݒ肷��H �ړ�������
            �h�� = 1 << 3,// �����o��������ݒ�ł���悤�ɂ���H ���̏�Ŋ�{�K�[�h�����ǁA���肪�����炩���ꂽ�瓮���o���A�I��
            �x�� = 1 << 4,
            �� = 1 << 5,
            �^�[�Q�b�g�ύX = 1 << 6, // �^�[�Q�b�g��ύX����B
            ���[�h�ύX = 1 << 7, // ���[�h�̕ύX
        }

        /// <summary>
        /// �L�����N�^�[�̑����B
        /// �����ɓ��Ă͂܂镪�S������ăr�b�g�t���O�`�F�b�N�B
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
            �퓬��� = 1 << 16, // �퓬���̃L����
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
            //��ѓ���U�� = 1 << 5, // ��ѓ���̌��m�͎��E�Z���T�[�Ɉ�C
            �o�t�G���A = 1 << 5, // �o�t�G���A��F��
            �f�o�t�G���A = 1 << 6, // �f�o�t�G���A��F��
            ���� = 1 << 7, // �����F��
            �ŏ� = 1 << 8, // �ŏ���F��
            �_���[�W�G���A = 1 << 9, // �_���[�W�G���A��F��
            �j��\�I�u�W�F�N�g = 1 << 10, // �j��\�ȃI�u�W�F�N�g��F��
            �悶�o��|�C���g = 1 << 11, // �R��F��
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

            /// <summary>
            /// ���F�������ōł��߂��G����̍U���I�u�W�F�N�g�̋���
            /// </summary>
            public float detectNearestAttackDistance;

            /// <summary>
            /// �����□���ɍU�����Ă����G�̃n�b�V���l
            /// ����͍U���X�R�A����ԑ傫���G
            /// �����̏ꍇ�̓_���[�W�̒l�����A�����̏ꍇ�̓_���[�W�̔���
            /// �񕜂�x���͖�������B
            /// �w�C�g����̏ꍇ�͂��̒l��1.2�{�A�����̏ꍇ��0.8�{�ɂ����X�R�A�ŕ]��
            /// </summary>
            public int hateEnemyHash;

            /// <summary>
            /// �Ō�Ɏ����ɍU�����Ă�������̃n�b�V���l�B
            /// </summary>
            public int attackerHash;

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

        #region ���s���L�����N�^�[���֘A�̍\���̒�`

        /// <summary>
        /// BaseImfo region - �L�����N�^�[�̊�{���iHP�AMP�A�ʒu�j
        /// �T�C�Y: 26�o�C�g
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
            public byte hpRatio;

            /// <summary>
            /// MP�̊���
            /// </summary>
            public byte mpRatio;

            /// <summary>
            /// ���݈ʒu
            /// </summary>
            public float2 nowPosition;

            /// <summary>
            /// HP/MP�������X�V����
            /// </summary>
            public void UpdateRatios()
            {
                this.hpRatio = (byte)(this.maxHp > 0 ? this.currentHp * 100 / this.maxHp : 0);
                this.mpRatio = (byte)(this.maxMp > 0 ? this.currentMp * 100 / this.maxMp : 0);
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
        /// 50byte
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CharacterColdLog
        {
            /// <summary>
            /// �L�����N�^�[�̃}�X�^�|�f�[�^���ID
            /// </summary>
            public readonly byte characterID;

            /// <summary>
            /// �L�����N�^�[�̃n�b�V���l��ۑ����Ă����B
            /// </summary>
            public int hashCode;

            /// <summary>
            /// �Ō�ɔ��f�������ԁB
            /// x���^�[�Q�b�g���f��y���s�����f�Az���ړ����f�̊Ԋu�B
            /// w���g���K�[���f�̊Ԋu
            /// </summary>
            public float4 lastJudgeTime;

            /// <summary>
            /// ���݂̃��[�h
            /// </summary>
            public byte nowMode; // ���݂̃��[�h�B���[�h�ύX���ɍX�V�����

            /// <summary>
            /// ���݂̃N�[���^�C���̃f�[�^�B
            /// </summary>
            [HideInInspector]
            public CoolTimeData nowCoolTime;

            public CharacterColdLog(BrainStatus status, int hash)
            {
                this.characterID = (byte)status.characterID;
                this.hashCode = hash;
                // �ŏ��̓}�C�i�X��10000�����邱�Ƃł���������悤��
                this.lastJudgeTime = -10000;
                this.nowMode = 0;
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

        #region ���f�֘A(Job�g�p)

        /// <summary>
        /// AI�ݒ�f�[�^��Job System�p�\����
        /// 
        /// �L�����N�^�[��AI���f�ɕK�v�ȃf�[�^��Job System�Ō����I�Ɏg�p�ł���悤�A
        /// �W���O�z����t���b�g�����ĕێ����܂��B
        /// �e�L�����N�^�[�̃f�[�^��ID���i1�x�[�X�j�Ń}�b�s���O����Ă��܂��B
        /// 
        /// �g�p���@�F
        /// - ����������CharacterModeData�̃W���O�z���n��
        /// - �eJob�ŕK�v�ȃf�[�^��Get���\�b�h�Ŏ擾
        /// - ReadOnly�ł̎g�p�𐄏�
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BrainDataForJob : IDisposable
        {
            #region ���f�Ԋu�f�[�^

            /// <summary>
            /// �S�L�����N�^�[�̔��f�Ԋu�f�[�^�i�t���b�g���ς݁j
            /// x: �^�[�Q�b�g���f�Ԋu
            /// y: �s�����f�Ԋu
            /// z: �ړ����f�Ԋu
            /// </summary>
            private NativeArray<float3> _intervalData;

            /// <summary>
            /// �e�L�����N�^�[�̔��f�Ԋu�f�[�^�J�n�ʒu
            /// �C���f�b�N�X = �L�����N�^�[ID - 1
            /// �l = _intervalDataIndexRangeData���ł̊J�n�C���f�b�N�X
            /// </summary>
            private NativeArray<int> _intervalDataIndexRangeStart;

            /// <summary>
            /// �e�L�����N�^�[�E�e���[�h�̔��f�Ԋu�f�[�^�ʒu���
            /// x: _intervalData���ł̊J�n�C���f�b�N�X
            /// y: �f�[�^���i�ʏ��1�j
            /// </summary>
            private NativeArray<int2> _intervalDataIndexRangeData;

            /// <summary>
            /// �V�X�^�[��p�F���f�Ԋu�f�[�^�i�ő�5���[�h���j
            /// </summary>
            private NativeArray<float3> _sisIntervalData;

            #endregion

            #region �g���K�[�s���f�[�^

            /// <summary>
            /// �S�L�����N�^�[�̃g���K�[�s�������f�[�^�i�t���b�g���ς݁j
            /// �D��x���Ɋi�[�i�ŏ��̗v�f�قǗD��x�������j
            /// </summary>
            private NativeArray<TriggerJudgeData> _triggerCondition;

            /// <summary>
            /// �e�L�����N�^�[�̃g���K�[�f�[�^�J�n�ʒu
            /// �C���f�b�N�X = �L�����N�^�[ID - 1
            /// �l = _triggerDataIndexRangeData���ł̊J�n�C���f�b�N�X
            /// </summary>
            private NativeArray<int> _triggerDataIndexRangeStart;

            /// <summary>
            /// �e�L�����N�^�[�E�e���[�h�̃g���K�[�f�[�^�ʒu���
            /// x: _triggerCondition���ł̊J�n�C���f�b�N�X
            /// y: �f�[�^��
            /// </summary>
            private NativeArray<int2> _triggerDataIndexRangeData;

            /// <summary>
            /// �V�X�^�[��p�F�g���K�[�s�������f�[�^�i�ő�5���[�h���j
            /// </summary>
            private NativeArray<TriggerJudgeData> _sisTriggerCondition;

            /// <summary>
            /// �V�X�^�[��p�F�e���[�h�̏I���C���f�b�N�X�i�ݐρj
            /// �ő�5���[�h�܂őΉ��B
            /// x: ���[�h1�̏I���ʒu�i0����J�n�j
            /// y: ���[�h2�̏I���ʒu�ix����J�n�j
            /// z: ���[�h3�̏I���ʒu�iy����J�n�j
            /// w: ���[�h4�̏I���ʒu�iz����J�n�j
            /// ���[�h5: w����z�񒷂܂�
            /// </summary>
            private int4 _sisTriggerIndexRange;

            #endregion

            #region �^�[�Q�b�g���f�f�[�^

            /// <summary>
            /// �S�L�����N�^�[�̃^�[�Q�b�g�I�������f�[�^�i�t���b�g���ς݁j
            /// �f�t�H���g�̓w�C�g�x�[�X�A�����w�莞�͍s�����Z�b�g�Ō���
            /// </summary>
            private NativeArray<TargetJudgeData> _targetCondition;

            /// <summary>
            /// �e�L�����N�^�[�̃^�[�Q�b�g�f�[�^�J�n�ʒu
            /// �C���f�b�N�X = �L�����N�^�[ID - 1
            /// �l = _tDataIndexRangeData���ł̊J�n�C���f�b�N�X
            /// </summary>
            private NativeArray<int> _tDataIndexRangeStart;

            /// <summary>
            /// �e�L�����N�^�[�E�e���[�h�̃^�[�Q�b�g�f�[�^�ʒu���
            /// x: _targetCondition���ł̊J�n�C���f�b�N�X
            /// y: �f�[�^��
            /// </summary>
            private NativeArray<int2> _tDataIndexRangeData;

            /// <summary>
            /// �V�X�^�[��p�F�^�[�Q�b�g�I�������f�[�^�i�ő�5���[�h���j
            /// </summary>
            private NativeArray<TargetJudgeData> _sisTargetCondition;

            /// <summary>
            /// �V�X�^�[��p�F�e���[�h�̏I���C���f�b�N�X�i�ݐρj
            /// �\����_sisTriggerIndexRange�Ɠ��l�i�ő�5���[�h�Ή��j
            /// </summary>
            private int4 _sisTargetIndexRange;

            #endregion

            #region �s�����f�f�[�^

            /// <summary>
            /// �S�L�����N�^�[�̍s���I�������f�[�^�i�t���b�g���ς݁j
            /// �D��x���Ɋi�[�i�ŏ��̗v�f�قǗD��x�������j
            /// </summary>
            private NativeArray<ActJudgeData> _actCondition;

            /// <summary>
            /// �e�L�����N�^�[�̍s���f�[�^�J�n�ʒu
            /// �C���f�b�N�X = �L�����N�^�[ID - 1
            /// �l = _actDataIndexRangeData���ł̊J�n�C���f�b�N�X
            /// </summary>
            private NativeArray<int> _actDataIndexRangeStart;

            /// <summary>
            /// �e�L�����N�^�[�E�e���[�h�̍s���f�[�^�ʒu���
            /// x: _actCondition���ł̊J�n�C���f�b�N�X
            /// y: �f�[�^��
            /// </summary>
            private NativeArray<int2> _actDataIndexRangeData;

            /// <summary>
            /// �V�X�^�[��p�F�s���I�������f�[�^�i�ő�5���[�h���j
            /// </summary>
            private NativeArray<ActJudgeData> _sisActCondition;

            /// <summary>
            /// �V�X�^�[��p�F�e���[�h�̏I���C���f�b�N�X�i�ݐρj
            /// �\����_sisTriggerIndexRange�Ɠ��l�i�ő�5���[�h�Ή��j
            /// </summary>
            private int4 _sisActIndexRange;

            #endregion

            /// <summary>
            /// CharacterModeData�̃W���O�z�񂩂�Job�p�̃t���b�g���f�[�^���\�z
            /// 
            /// �f�[�^�\���F
            /// - sourceData[0�`n-2]: �ʏ�L�����N�^�[�̃f�[�^
            /// - sourceData[n-1]: �V�X�^�[�̃f�[�^�i���ʏ����j
            /// 
            /// �t���b�g���̗���F
            /// 1. �e�L�����N�^�[�̊e���[�h�̃f�[�^���ꎟ���z��ɓW�J
            /// 2. �C���f�b�N�X�Ǘ��p�̔z��Ŋe�f�[�^�̈ʒu���L�^
            /// 3. GetSubArray�ō����A�N�Z�X�\�ȍ\��������
            /// </summary>
            /// <param name="sourceData">�L�����N�^�[���[�h�f�[�^�̃W���O�z��i�l�^�\���̂̔z��j</param>
            /// <param name="allocator">�������A���P�[�^�i�f�t�H���g: Persistent�j</param>
            public BrainDataForJob(CharacterModeData[][] sourceData, Allocator allocator = Allocator.Persistent)
            {
                // �Ō�̗v�f�̓V�X�^�[�̃f�[�^�Ƃ��ē��ʈ���
                int normalCharCount = sourceData.Length - 1;

                #region ���f�Ԋu�f�[�^�̏�����

                // ===== �ꎞ�I�ȃR���e�i�̏��� =====
                // �z�肳���ő�T�C�Y�ŏ������i�L������ �~ �ő僂�[�h��6�j
                var intervalDataContainer = new UnsafeList<float3>(normalCharCount * 6, Allocator.Temp);
                var intervalDataRangeStartContainer = new UnsafeList<int>(normalCharCount, Allocator.Temp);
                var intervalDataRangeContainer = new UnsafeList<int2>(normalCharCount * 6, Allocator.Temp);

                // ===== �ʏ�L�����N�^�[�̔��f�Ԋu�f�[�^���t���b�g�� =====
                for ( int charId = 0; charId < normalCharCount; charId++ )
                {
                    // ���̃L�����N�^�[�̃��[�h�͈͏��̊J�n�ʒu���L�^
                    // ��: charId=0�Ȃ�0�AcharId=1�őO�L������3���[�h�Ȃ�3
                    intervalDataRangeStartContainer.Add(intervalDataRangeContainer.Length);

                    // �e���[�h�̃f�[�^�������ǉ�
                    for ( int mode = 0; mode < sourceData[charId].Length; mode++ )
                    {
                        // ���݂̃f�[�^�ʒu�ƒ����i���f�Ԋu��1�v�f�̂݁j���L�^
                        intervalDataRangeContainer.Add(new int2(intervalDataContainer.Length, 1));

                        // ���ۂ̃f�[�^��ǉ�
                        intervalDataContainer.Add(sourceData[charId][mode].judgeInterval);
                    }
                }

                // ===== �ꎞ�R���e�i����i���I��NativeArray�ɕϊ� =====
                // ToArray()��UnsafeList�̓����z��ւ̎Q�Ƃ��擾���ANativeArray�ɃR�s�[
                _intervalData = new NativeArray<float3>(intervalDataContainer.ToArray(), allocator);
                _intervalDataIndexRangeStart = new NativeArray<int>(intervalDataRangeStartContainer.ToArray(), allocator);
                _intervalDataIndexRangeData = new NativeArray<int2>(intervalDataRangeContainer.ToArray(), allocator);

                // ===== �V�X�^�[�̔��f�Ԋu�f�[�^��ݒ� =====
                // �V�X�^�[�͓��ʈ����̂��߁A�Ɨ������z��ŊǗ�
                var sisIntervalList = new List<float3>();
                for ( int mode = 0; mode < sourceData[normalCharCount].Length; mode++ )
                {
                    sisIntervalList.Add(sourceData[normalCharCount][mode].judgeInterval);
                }
                _sisIntervalData = new NativeArray<float3>(sisIntervalList.ToArray(), allocator);

                #endregion

                #region �g���K�[�s���f�[�^�̏�����

                // ===== �ꎞ�I�ȃR���e�i�̏��� =====
                // �g���K�[�f�[�^�͉ϒ��̂��߁A�傫�߂ɏ�����
                var triggerDataContainer = new UnsafeList<TriggerJudgeData>(normalCharCount * 10, Allocator.Temp);
                var triggerDataRangeStartContainer = new UnsafeList<int>(normalCharCount, Allocator.Temp);
                var triggerDataRangeContainer = new UnsafeList<int2>(normalCharCount * 6, Allocator.Temp);

                // ===== �ʏ�L�����N�^�[�̃g���K�[�f�[�^���t���b�g�� =====
                for ( int charId = 0; charId < normalCharCount; charId++ )
                {
                    // ���̃L�����N�^�[�̃C���f�b�N�X�͈͏��̊J�n�ʒu���L�^
                    triggerDataRangeStartContainer.Add(triggerDataRangeContainer.Length);

                    // �e���[�h�̃g���K�[����������
                    for ( int mode = 0; mode < sourceData[charId].Length; mode++ )
                    {
                        // ���̃��[�h�̃g���K�[�f�[�^�J�n�ʒu���L�^
                        int startIndex = triggerDataContainer.Length;

                        // ���[�h���̑S�g���K�[������ǉ�
                        var triggerConditions = sourceData[charId][mode].triggerCondition;
                        foreach ( var trigger in triggerConditions )
                        {
                            triggerDataContainer.Add(trigger);
                        }

                        // ���̃��[�h�̃f�[�^�͈́i�J�n�ʒu�Ɨv�f���j���L�^
                        triggerDataRangeContainer.Add(new int2(startIndex, triggerConditions.Length));
                    }
                }

                // ===== NativeArray�ɕϊ� =====
                _triggerCondition = new NativeArray<TriggerJudgeData>(triggerDataContainer.ToArray(), allocator);
                _triggerDataIndexRangeStart = new NativeArray<int>(triggerDataRangeStartContainer.ToArray(), allocator);
                _triggerDataIndexRangeData = new NativeArray<int2>(triggerDataRangeContainer.ToArray(), allocator);

                // ===== �V�X�^�[�̃g���K�[�f�[�^��ݐσC���f�b�N�X�����Őݒ� =====
                var sisTriggerList = new List<TriggerJudgeData>();
                var sisTriggerRanges = new int4();
                int currentEndIndex = 0;

                // �ő�5���[�h�܂ŏ����iint4�̐����ɂ��j
                for ( int mode = 0; mode < sourceData[normalCharCount].Length && mode < 4; mode++ )
                {
                    var triggers = sourceData[normalCharCount][mode].triggerCondition;
                    sisTriggerList.AddRange(triggers);

                    // �ݐϏI���ʒu���X�V
                    currentEndIndex += triggers.Length;

                    // int4�̊e�v�f�ɗݐϏI���C���f�b�N�X��ݒ�
                    // ����ɂ��A���[�h�Ԃ̋��E�������I�ɊǗ�
                    switch ( mode )
                    {
                        case 0:
                            sisTriggerRanges.x = currentEndIndex;
                            break;  // ���[�h1: 0�`x
                        case 1:
                            sisTriggerRanges.y = currentEndIndex;
                            break;  // ���[�h2: x�`y
                        case 2:
                            sisTriggerRanges.z = currentEndIndex;
                            break;  // ���[�h3: y�`z
                        case 3:
                            sisTriggerRanges.w = currentEndIndex;
                            break;  // ���[�h4: z�`w
                                    // ���[�h5: w�`�z�񒷁i�����I�Ɍ���j
                    }
                }

                _sisTriggerCondition = new NativeArray<TriggerJudgeData>(sisTriggerList.ToArray(), allocator);
                _sisTriggerIndexRange = sisTriggerRanges;

                #endregion

                #region �^�[�Q�b�g���f�f�[�^�̏�����

                // ===== �ꎞ�I�ȃR���e�i�̏��� =====
                var targetDataContainer = new UnsafeList<TargetJudgeData>(normalCharCount * 10, Allocator.Temp);
                var targetDataRangeStartContainer = new UnsafeList<int>(normalCharCount, Allocator.Temp);
                var targetDataRangeContainer = new UnsafeList<int2>(normalCharCount * 6, Allocator.Temp);

                // ===== �ʏ�L�����N�^�[�̃^�[�Q�b�g�f�[�^���t���b�g�� =====
                // �g���K�[�f�[�^�Ɠ����p�^�[���ŏ���
                for ( int charId = 0; charId < normalCharCount; charId++ )
                {
                    targetDataRangeStartContainer.Add(targetDataRangeContainer.Length);

                    for ( int mode = 0; mode < sourceData[charId].Length; mode++ )
                    {
                        int startIndex = targetDataContainer.Length;
                        var targetConditions = sourceData[charId][mode].targetCondition;

                        // �S�^�[�Q�b�g�������t���b�g�z��ɒǉ�
                        foreach ( var target in targetConditions )
                        {
                            targetDataContainer.Add(target);
                        }

                        targetDataRangeContainer.Add(new int2(startIndex, targetConditions.Length));
                    }
                }

                // ===== NativeArray�ɕϊ� =====
                _targetCondition = new NativeArray<TargetJudgeData>(targetDataContainer.ToArray(), allocator);
                _tDataIndexRangeStart = new NativeArray<int>(targetDataRangeStartContainer.ToArray(), allocator);
                _tDataIndexRangeData = new NativeArray<int2>(targetDataRangeContainer.ToArray(), allocator);

                // ===== �V�X�^�[�̃^�[�Q�b�g�f�[�^��ݒ� =====
                var sisTargetList = new List<TargetJudgeData>();
                var sisTargetRanges = new int4();
                currentEndIndex = 0;

                for ( int mode = 0; mode < sourceData[normalCharCount].Length && mode < 4; mode++ )
                {

                    var targets = sourceData[normalCharCount][mode].targetCondition;
                    sisTargetList.AddRange(targets);
                    currentEndIndex += targets.Length;

                    switch ( mode )
                    {
                        case 0:
                            sisTargetRanges.x = currentEndIndex;
                            break;
                        case 1:
                            sisTargetRanges.y = currentEndIndex;
                            break;
                        case 2:
                            sisTargetRanges.z = currentEndIndex;
                            break;
                        case 3:
                            sisTargetRanges.w = currentEndIndex;
                            break;
                    }
                }

                _sisTargetCondition = new NativeArray<TargetJudgeData>(sisTargetList.ToArray(), allocator);
                _sisTargetIndexRange = sisTargetRanges;

                #endregion

                #region �s�����f�f�[�^�̏�����

                // ===== �ꎞ�I�ȃR���e�i�̏��� =====
                var actDataContainer = new UnsafeList<ActJudgeData>(normalCharCount * 10, Allocator.Temp);
                var actDataRangeStartContainer = new UnsafeList<int>(normalCharCount, Allocator.Temp);
                var actDataRangeContainer = new UnsafeList<int2>(normalCharCount * 6, Allocator.Temp);

                // ===== �ʏ�L�����N�^�[�̍s���f�[�^���t���b�g�� =====
                for ( int charId = 0; charId < normalCharCount; charId++ )
                {
                    actDataRangeStartContainer.Add(actDataRangeContainer.Length);

                    for ( int mode = 0; mode < sourceData[charId].Length; mode++ )
                    {
                        int startIndex = actDataContainer.Length;
                        var actConditions = sourceData[charId][mode].actCondition;

                        // �S�s���������t���b�g�z��ɒǉ�
                        foreach ( var act in actConditions )
                        {
                            actDataContainer.Add(act);
                        }

                        actDataRangeContainer.Add(new int2(startIndex, actConditions.Length));
                    }
                }

                // ===== NativeArray�ɕϊ� =====
                _actCondition = new NativeArray<ActJudgeData>(actDataContainer.ToArray(), allocator);
                _actDataIndexRangeStart = new NativeArray<int>(actDataRangeStartContainer.ToArray(), allocator);
                _actDataIndexRangeData = new NativeArray<int2>(actDataRangeContainer.ToArray(), allocator);

                // ===== �V�X�^�[�̍s���f�[�^��ݒ� =====
                var sisActList = new List<ActJudgeData>();
                var sisActRanges = new int4();
                currentEndIndex = 0;

                for ( int mode = 0; mode < sourceData[normalCharCount].Length && mode < 4; mode++ )
                {
                    var acts = sourceData[normalCharCount][mode].actCondition;
                    sisActList.AddRange(acts);
                    currentEndIndex += acts.Length;

                    switch ( mode )
                    {
                        case 0:
                            sisActRanges.x = currentEndIndex;
                            break;
                        case 1:
                            sisActRanges.y = currentEndIndex;
                            break;
                        case 2:
                            sisActRanges.z = currentEndIndex;
                            break;
                        case 3:
                            sisActRanges.w = currentEndIndex;
                            break;
                    }
                }

                _sisActCondition = new NativeArray<ActJudgeData>(sisActList.ToArray(), allocator);
                _sisActIndexRange = sisActRanges;

                #endregion

                // ===== �ꎞ�I�ȃR���e�i����� =====
                // Allocator.Temp�Ŋm�ۂ����������͖����I�ɉ��
                intervalDataContainer.Dispose();
                intervalDataRangeStartContainer.Dispose();
                intervalDataRangeContainer.Dispose();
                triggerDataContainer.Dispose();
                triggerDataRangeStartContainer.Dispose();
                triggerDataRangeContainer.Dispose();
                targetDataContainer.Dispose();
                targetDataRangeStartContainer.Dispose();
                targetDataRangeContainer.Dispose();
                actDataContainer.Dispose();
                actDataRangeStartContainer.Dispose();
                actDataRangeContainer.Dispose();
            }

            /// <summary>
            /// �w�肳�ꂽ�L�����N�^�[ID�E���[�h�̔��f�Ԋu�f�[�^���擾
            /// </summary>
            /// <param name="id">�L�����N�^�[ID�i1�x�[�X�j</param>
            /// <param name="mode">���[�h�ԍ��i1�x�[�X�j</param>
            /// <returns>���f�Ԋu�f�[�^�ix:�^�[�Q�b�g, y:�s��, z:�ړ��j</returns>
            public float3 GetIntervalData(byte id, byte mode)
            {
                id--;
                mode--;

                // �V�X�^�[�̏ꍇ
                if ( id >= _intervalDataIndexRangeStart.Length )
                {
                    if ( mode >= 0 && mode < _sisIntervalData.Length )
                    {
                        return _sisIntervalData[mode];
                    }
                    return float3.zero;
                }

                // �ʏ�L�����N�^�[�̏ꍇ
                if ( id >= 0 && id < _intervalDataIndexRangeStart.Length )
                {
                    int2 indexData = _intervalDataIndexRangeData[_intervalDataIndexRangeStart[id] + mode];
                    return _intervalData[indexData.x];
                }

                return float3.zero;
            }

            /// <summary>
            /// �w�肳�ꂽ�L�����N�^�[ID�E���[�h�̃g���K�[���f�f�[�^�z����擾
            /// </summary>
            /// <param name="id">�L�����N�^�[ID�i1�x�[�X�j</param>
            /// <param name="mode">���[�h�ԍ��i1�x�[�X�j</param>
            /// <returns>�g���K�[���f�f�[�^�̔z��i�D��x���j</returns>
            public NativeArray<TriggerJudgeData> GetTriggerJudgeDataArray(int id, int mode)
            {
                id--;
                mode--;

                // �V�X�^�[�̏ꍇ�i�ő�5���[�h�Ή��j
                if ( id >= _triggerDataIndexRangeStart.Length )
                {
                    int startIndex = 0;
                    int endIndex = 0;

                    switch ( mode )
                    {
                        case 0:
                            startIndex = 0;
                            endIndex = _sisTriggerIndexRange.x;
                            break;
                        case 1:
                            startIndex = _sisTriggerIndexRange.x;
                            endIndex = _sisTriggerIndexRange.y;
                            break;
                        case 2:
                            startIndex = _sisTriggerIndexRange.y;
                            endIndex = _sisTriggerIndexRange.z;
                            break;
                        case 3:
                            startIndex = _sisTriggerIndexRange.z;
                            endIndex = _sisTriggerIndexRange.w;
                            break;
                        case 4:
                            startIndex = _sisTriggerIndexRange.w;
                            endIndex = _sisTriggerCondition.Length;
                            break;
                        default:
                            return new NativeArray<TriggerJudgeData>();
                    }

                    return _sisTriggerCondition.GetSubArray(startIndex, endIndex - startIndex);
                }

                // �ʏ�L�����N�^�[�̏ꍇ
                if ( id >= 0 && id < _triggerDataIndexRangeStart.Length )
                {
                    int2 indexData = _triggerDataIndexRangeData[_triggerDataIndexRangeStart[id] + mode];
                    return _triggerCondition.GetSubArray(indexData.x, indexData.y);
                }

                return new NativeArray<TriggerJudgeData>();
            }

            /// <summary>
            /// �w�肳�ꂽ�L�����N�^�[ID�E���[�h�̃^�[�Q�b�g���f�f�[�^�z����擾
            /// </summary>
            /// <param name="id">�L�����N�^�[ID�i1�x�[�X�j</param>
            /// <param name="mode">���[�h�ԍ��i1�x�[�X�j</param>
            /// <returns>�^�[�Q�b�g���f�f�[�^�̔z��</returns>
            public NativeArray<TargetJudgeData> GetTargetJudgeDataArray(int id, int mode)
            {
                id--;
                mode--;

                // �V�X�^�[�̏ꍇ�i�ő�5���[�h�Ή��j
                if ( id >= _tDataIndexRangeStart.Length )
                {
                    int startIndex = 0;
                    int endIndex = 0;

                    switch ( mode )
                    {
                        case 0:
                            startIndex = 0;
                            endIndex = _sisTargetIndexRange.x;
                            break;
                        case 1:
                            startIndex = _sisTargetIndexRange.x;
                            endIndex = _sisTargetIndexRange.y;
                            break;
                        case 2:
                            startIndex = _sisTargetIndexRange.y;
                            endIndex = _sisTargetIndexRange.z;
                            break;
                        case 3:
                            startIndex = _sisTargetIndexRange.z;
                            endIndex = _sisTargetIndexRange.w;
                            break;
                        case 4:
                            startIndex = _sisTargetIndexRange.w;
                            endIndex = _sisTargetCondition.Length;
                            break;
                        default:
                            return new NativeArray<TargetJudgeData>();
                    }

                    return _sisTargetCondition.GetSubArray(startIndex, endIndex - startIndex);
                }

                // �ʏ�L�����N�^�[�̏ꍇ
                if ( id >= 0 && id < _tDataIndexRangeStart.Length )
                {
                    int2 indexData = _tDataIndexRangeData[_tDataIndexRangeStart[id] + mode];
                    return _targetCondition.GetSubArray(indexData.x, indexData.y);
                }

                return new NativeArray<TargetJudgeData>();
            }

            /// <summary>
            /// �w�肳�ꂽ�L�����N�^�[ID�E���[�h�̍s�����f�f�[�^�z����擾
            /// </summary>
            /// <param name="id">�L�����N�^�[ID�i1�x�[�X�j</param>
            /// <param name="mode">���[�h�ԍ��i1�x�[�X�j</param>
            /// <returns>�s�����f�f�[�^�̔z��i�D��x���j</returns>
            public NativeArray<ActJudgeData> GetActJudgeDataArray(int id, int mode)
            {
                id--;
                mode--;

                // �V�X�^�[�̏ꍇ�i�ő�5���[�h�Ή��j
                if ( id >= _actDataIndexRangeStart.Length )
                {
                    int startIndex = 0;
                    int endIndex = 0;

                    switch ( mode )
                    {
                        case 0:
                            startIndex = 0;
                            endIndex = _sisActIndexRange.x;
                            break;
                        case 1:
                            startIndex = _sisActIndexRange.x;
                            endIndex = _sisActIndexRange.y;
                            break;
                        case 2:
                            startIndex = _sisActIndexRange.y;
                            endIndex = _sisActIndexRange.z;
                            break;
                        case 3:
                            startIndex = _sisActIndexRange.z;
                            endIndex = _sisActIndexRange.w;
                            break;
                        case 4:
                            startIndex = _sisActIndexRange.w;
                            endIndex = _sisActCondition.Length;
                            break;
                        default:
                            return new NativeArray<ActJudgeData>();
                    }

                    return _sisActCondition.GetSubArray(startIndex, endIndex - startIndex);
                }

                // �ʏ�L�����N�^�[�̏ꍇ
                if ( id >= 0 && id < _actDataIndexRangeStart.Length )
                {
                    int2 indexData = _actDataIndexRangeData[_actDataIndexRangeStart[id] + mode];
                    return _actCondition.GetSubArray(indexData.x, indexData.y);
                }

                return new NativeArray<ActJudgeData>();
            }

            /// <summary>
            /// �S�Ă�NativeArray�����
            /// �A�v���P�[�V�����I�����܂��̓f�[�^�s�v���ɕK���Ăяo������
            /// </summary>
            public void Dispose()
            {
                // ���f�Ԋu�f�[�^�̉��
                if ( _intervalData.IsCreated )
                    _intervalData.Dispose();
                if ( _intervalDataIndexRangeStart.IsCreated )
                    _intervalDataIndexRangeStart.Dispose();
                if ( _intervalDataIndexRangeData.IsCreated )
                    _intervalDataIndexRangeData.Dispose();
                if ( _sisIntervalData.IsCreated )
                    _sisIntervalData.Dispose();

                // �g���K�[�s���f�[�^�̉��
                if ( _triggerCondition.IsCreated )
                    _triggerCondition.Dispose();
                if ( _triggerDataIndexRangeStart.IsCreated )
                    _triggerDataIndexRangeStart.Dispose();
                if ( _triggerDataIndexRangeData.IsCreated )
                    _triggerDataIndexRangeData.Dispose();
                if ( _sisTriggerCondition.IsCreated )
                    _sisTriggerCondition.Dispose();

                // �^�[�Q�b�g���f�f�[�^�̉��
                if ( _targetCondition.IsCreated )
                    _targetCondition.Dispose();
                if ( _tDataIndexRangeStart.IsCreated )
                    _tDataIndexRangeStart.Dispose();
                if ( _tDataIndexRangeData.IsCreated )
                    _tDataIndexRangeData.Dispose();
                if ( _sisTargetCondition.IsCreated )
                    _sisTargetCondition.Dispose();

                // �s�����f�f�[�^�̉��
                if ( _actCondition.IsCreated )
                    _actCondition.Dispose();
                if ( _actDataIndexRangeStart.IsCreated )
                    _actDataIndexRangeStart.Dispose();
                if ( _actDataIndexRangeData.IsCreated )
                    _actDataIndexRangeData.Dispose();
                if ( _sisActCondition.IsCreated )
                    _sisActCondition.Dispose();
            }
        }

        /// <summary>
        /// �s����̃N�[���^�C���L�����Z�����f�Ɏg�p����f�[�^�B
        /// SoA OK
        /// 32Byte
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CoolTimeData
        {
            /// <summary>
            /// �s��������X�L�b�v�������
            /// </summary>
            [Header("�s��������X�L�b�v�������")]
            public ActTriggerCondition skipCondition;

            /// <summary>
            /// ���f�Ɏg�p���鐔�l�B
            /// �����ɂ���Ă�enum��ϊ��������������肷��B
            /// ���̐��l�ȏ�̃f�[�^������΃N�[���^�C�����X�L�b�v����B
            /// </summary>
            [Header("��ƂȂ�l")]
            public int judgeLowerValue;

            /// <summary>
            /// ���f�Ɏg�p���鐔�l�B
            /// �����ɂ���Ă�enum��ϊ��������������肷��B
            /// ���̐��l�ȉ��̃f�[�^������΃N�[���^�C�����X�L�b�v����B
            /// </summary>
            [Header("��ƂȂ�l")]
            public int judgeUpperValue;

            /// <summary>
            /// �ݒ肷��N�[���^�C���B
            /// </summary>
            public float coolTime;

            /// <summary>
            /// �Ώۂ̐w�c�敪
            /// �����w�肠��
            /// </summary>
            [Header("�`�F�b�N�Ώۂ̏���")]
            public TargetFilter filter;

        }

        /// <summary>
        /// ���f�Ɏg�p����f�[�^�B
        /// ���̗v���𖞂����Ə����C�x���g���g���K�[�����B
        /// 0.5�b�Ɉ�񔻒�B
        /// 30Byte
        /// SoA OK
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct TriggerJudgeData
        {
            /// <summary>
            /// �g���K�[�����s���̃^�C�v
            /// </summary>
            public enum TriggerEventType : byte
            {
                ���[�h�ύX = 0,
                �^�[�Q�b�g�ύX = 1,// �^�[�Q�b�g�ύX�̍ۂ͗D�悷��^�[�Q�b�g�������w��ł���B
                �ʍs�� = 2,
            }

            /// <summary>
            /// �s������
            /// </summary>
            [Header("�s������̏���")]
            public ActTriggerCondition judgeCondition;

            /// <summary>
            /// 1����100�ŕ\������s�������s����\���B
            /// �������f���s���O�ɗ����Ŕ��������B
            /// 100�̏ꍇ�͏��������������100%���s����B
            /// </summary>
            public byte actRatio;

            /// <summary>
            /// ���f�Ɏg�p���鐔�l�B
            /// �����ɂ���Ă�enum��ϊ��������������肷��B
            /// ���̐��l�ȏ�̃f�[�^������΍s��������B
            /// </summary>
            [Header("��ƂȂ�l1")]
            public int judgeLowerValue;

            /// <summary>
            /// ���f�Ɏg�p���鐔�l�B
            /// �����ɂ���Ă�enum��ϊ��������������肷��B
            /// ���̐��l�ȉ��̃f�[�^������΍s��������B
            /// </summary>
            [Header("��ƂȂ�l2")]
            public int judgeUpperValue;

            /// <summary>
            /// �g���K�[�����C�x���g�̃^�C�v�B
            /// </summary>
            public TriggerEventType triggerEventType;

            /// <summary>
            /// �g���K�[�����s���̔ԍ���A���[�h�̃f�[�^�B
            /// �g���K�[�C�x���g�^�C�v�ɉ����ĈӖ����ς��B
            /// </summary>
            public byte triggerNum;

            /// <summary>
            /// �Ώۂ̐w�c�敪
            /// �����w�肠��
            /// </summary>
            [Header("�`�F�b�N�Ώۂ̏���")]
            public TargetFilter filter;
        }

        /// <summary>
        /// �^�[�Q�b�g��I������ۂɎg�p����f�[�^�B
        /// �w�C�g�ł�����ȊO�ł��\���͓̂���
        /// 21Byte 
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

        }

        /// <summary>
        /// �s�����f�Ɏg�p����f�[�^�B
        /// ���̗v���𖞂����Ɠ���̍s�����g���K�[�����B
        /// ���[�h�`�F���W�Ȃǂ������N������B
        /// 
        /// 29Byte
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
            public MoveSelectCondition judgeCondition;

            /// <summary>
            /// 1����100�ŕ\������s�������s����\���B
            /// �������f���s���O�ɗ����Ŕ��������B
            /// 100�̏ꍇ�͏��������������100%���s����B
            /// </summary>
            public byte actRatio;

            /// <summary>
            /// ���f�Ɏg�p���鐔�l�B
            /// �����ɂ���Ă�enum��ϊ��������������肷��B
            /// ���̐��l�ȏ�̃f�[�^������΍s��������B
            /// </summary>
            [Header("��ƂȂ�l1")]
            public int judgeLowerValue;

            /// <summary>
            /// ���f�Ɏg�p���鐔�l�B
            /// �����ɂ���Ă�enum��ϊ��������������肷��B
            /// ���̐��l�ȉ��̃f�[�^������΍s��������B
            /// </summary>
            [Header("��ƂȂ�l2")]
            public int judgeUpperValue;

            /// <summary>
            /// �g���K�[�����s���̃^�C�v�B
            /// </summary>
            public TriggerEventType triggerEventType;

            /// <summary>
            /// �g���K�[�����s���̔ԍ���A���[�h�̃f�[�^�B
            /// �g���K�[�C�x���g�^�C�v�ɉ����ĈӖ����ς��B
            /// </summary>
            public byte triggerNum;

            /// <summary>
            /// ���̃t���O���^�Ȃ�N�[���^�C�����ł����f���s���B
            /// </summary>
            public bool isCoolTimeIgnore;

            /// <summary>
            /// ���̃t���O���^�Ȃ画�f�͎����ɑ΂��čs���B
            /// </summary>
            public bool isSelfJudge;

            /// <summary>
            /// �Ώۂ̐w�c�敪
            /// �����w�肠��
            /// </summary>
            [Header("�`�F�b�N�Ώۂ̏���")]
            public TargetFilter filter;
        }

        /// <summary>
        /// �s��������Ώېݒ�����Ō����Ώۂ��t�B���^�[���邽�߂̍\����
        /// 19Byte
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
                �g�p�����t�B���^�[_And���f = 1 << 5,
                ������Ώۂɂ��� = 1 << 6,
                �v���[���[��Ώۂɂ��� = 1 << 7,
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
            /// ������Ώۂɂ��邩�ǂ���
            /// </summary>
            public bool SelfTarget
            {
                get => (this._filterFlags & FilterBitFlag.������Ώۂɂ���) != 0;
            }

            /// <summary>
            /// �v���C���[��Ώۂɂ��邩
            /// </summary>
            public bool PlayerTarget
            {
                get => (this._filterFlags & FilterBitFlag.�v���[���[��Ώۂɂ���) != 0;
            }

            /// <summary>
            /// �����ΏۃL�����N�^�[�̏����ɓ��Ă͂܂邩���`�F�b�N����B
            /// </summary>
            /// <param name="solidData"></param>
            /// <param name="stateInfo"></param>
            /// <returns></returns>
            [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
            [BurstCompile]
            public byte IsPassFilter(in SolidData solidData, in CharacterStateInfo stateInfo, float2 nowPosition, float2 targetPosition)
            {

                if ( _isSightCheck )
                {
                    RaycastCommand ray = new RaycastCommand(
                        (Vector2)nowPosition,
                        targetPosition - nowPosition,
                        0.1f, // �����`�F�b�N�̋���
                        LayerMask.GetMask("Default") // ���C���[�}�X�N�͓K�X�ύX
                    );

                }

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
            [BurstCompile]
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

        #region SoA�ΏۊO�\����

        #region �L�����N�^�[��f�[�^�i��Job�j

        /// <summary>
        /// �L�����N�^�[�̃��[�h���ǂ̂悤�Ȃ��̂ł��邩���`���邽�߂̍\���́B
        /// AI�Ŏg�p����f�[�^������B
        /// </summary>
        [Serializable]
        public class CharacterModeData
        {
            /// <summary>
            /// ���[�h���Ƃ̔��f�̊Ԋu
            /// x���^�[�Q�b�g���f��y���s�����f�Az���ړ����f�̊Ԋu�B
            /// </summary>
            public float3 judgeInterval;

            /// <summary>
            /// �U���ȊO�̍s�������f�[�^.
            /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
            /// </summary>
            [Header("�g���K�[�s�����f����")]
            public TriggerJudgeData[] triggerCondition;

            /// <summary>
            /// �^�[�Q�b�g���f�p�̃f�[�^�B
            /// </summary>
            [Header("�^�[�Q�b�g���f����")]
            public TargetJudgeData[] targetCondition;

            /// <summary>
            /// �U���ȊO�̍s�������f�[�^.
            /// �ŏ��̗v�f�قǗD��x�����̂ŏd�_�B
            /// </summary>
            [Header("�s�����f����")]
            public ActJudgeData[] actCondition;

        }

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
        /// 
        /// ���@�Ƃ��ړ��Ƃ��S������ɑg�ݍ��߂�悤�ɂ���B
        /// ����͍s���̃w�b�_���Ȃ̂ŁA���ۂ̍s���f�[�^�̓C���^�[�t�F�C�X���Ȃɂ��o�R�Ŕh���N���X�Ɏ������Ă������B
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Auto)]
        public struct ActData
        {
            /// <summary>
            /// �U���{���B
            /// �����郂�[�V�����l
            /// </summary>
            [Header("�U���{���i���[�V�����l�j")]
            public float motionValue;


            /// <summary>
            /// �s����̍d���Ɋւ���f�[�^�B
            /// �s���I����ɐݒ肷��B
            /// </summary>
            [Header("�s���C���^�[�o���f�[�^")]
            public CoolTimeData coolTimeData;

            /// <summary>
            /// �O�����牽�����Ă���̂��A��������悤�ɍs���ɉ����Đݒ肷��f�[�^�B
            /// </summary>
            [Header("�ύX��̍s���^�C�v")]
            public ActState stateChange;

            /// <summary>
            /// ���̍s����L�����Z�����Ĕ������邩�B
            /// </summary>
            public bool isCancel;
        }

        #endregion

        #endregion SoA�ΏۊO

        #endregion �\���̒�`

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
        /// �L�����N�^�[�̃��[�h���Ƃ�AI�ݒ�B
        /// </summary>
        [Header("AI�ݒ�")]
        public CharacterModeData[] characterModeSetting;

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
        public ActData[] attackData;
    }
}