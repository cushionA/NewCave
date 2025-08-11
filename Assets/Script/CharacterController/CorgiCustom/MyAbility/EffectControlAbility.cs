using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace CharacterController
{
    /// <summary>
    /// �G�t�F�N�g�^�C�v�̓��ꂳ�ꂽ�񋓌^�iLong�g�p�Ŋg�����m�ہj
    /// �r�b�g���Z�ŃJ�e�S���������\
    /// 
    /// �����˂����͂Ȃ��B
    /// ���̑���U������1,2�Ƃ����ĕʂ̃^�C�v�ŋ�������悤�Ɂi�G�t�F�N�g�͓����H�j
    /// </summary>
    [Flags]
    public enum EffectType : long
    {
        /// <summary>
        /// �G�t�F�N�g�Ȃ�
        /// </summary>
        �Ȃ� = 0,

        // === �f�o�t�i���̌��ʁj===
        // ��Ԉُ�n (0x8000_0000_0000_0000 �`)

        /// <summary>
        /// �ŁF�p���I��HP�����������Ԉُ�
        /// </summary>
        �� = 1L << 63,

        /// <summary>
        /// �ғŁF�ł����傫�ȃ_���[�W��^���A�X�^�~�i�񕜑��x���ቺ�������ʏ�Ԉُ�
        /// </summary>
        �ғ� = 1L << 62,

        /// <summary>
        /// �����F�L�����N�^�[���~��Ԃɂ��A��_���[�W�𑝑傳����
        /// </summary>
        ���� = 1L << 61,

        /// <summary>
        /// �S���F�L�����N�^�[���~��Ԃɂ���i���������ʎ��ԒZ�߁j
        /// </summary>
        �S�� = 1L << 60,

        /// <summary>
        /// ���فF���@�̎g�p�𕕈󂷂��Ԉُ�
        /// </summary>
        ���� = 1L << 59,

        /// <summary>
        /// ����F��_���[�W�㏸�A�X�^�~�i�E�A�[�}�[�񕜑��x�ቺ�A��Ԉُ�~�ω������x����
        /// </summary>
        ���� = 1L << 58,

        /// <summary>
        /// �߂܂��F�^�_���[�W�����A�K�[�h���\�򉻂������N����
        /// </summary>
        �߂܂� = 1L << 57,

        /// <summary>
        /// �����F�߂܂��̏�ʔłŁA��苭���^�_���[�W�����ƃK�[�h���\��
        /// </summary>
        ���� = 1L << 56,

        /// <summary>
        /// ����F�w�C�g�㏸�ƈړ����x�ቺ�𓯎��Ɉ����N����
        /// </summary>
        ���� = 1L << 55,

        // �X�e�[�^�X�ቺ�n (0x4000_0000_0000_0000 �`)

        /// <summary>
        /// �U���͒ቺ�F�^����_���[�W����������
        /// </summary>
        �U���͒ቺ = 1L << 54,

        /// <summary>
        /// �h��͒ቺ�F�����E���@�h��͂���������
        /// </summary>
        �h��͒ቺ = 1L << 53,

        /// <summary>
        /// �ړ����x�ቺ�F�L�����N�^�[�̈ړ����x����������
        /// </summary>
        �ړ����x�ቺ = 1L << 52,

        /// <summary>
        /// ��_���[�W����F�󂯂�_���[�W����������i�����i�f�����b�g�ɂ��g�p�j
        /// </summary>
        ��_���[�W���� = 1L << 51,

        /// <summary>
        /// �^�_���[�W�����F�^����_���[�W����������i�����i�f�����b�g�ɂ��g�p�j
        /// </summary>
        �^�_���[�W���� = 1L << 50,

        /// <summary>
        /// �w�C�g�㏸�F�G����̒��ړx�i�w�C�g�l�j���㏸����
        /// </summary>
        �w�C�g�㏸ = 1L << 49,

        /// <summary>
        /// �A�C�e�����ʌ����F�񕜃A�C�e���⋭���A�C�e���̌��ʂ���������
        /// </summary>
        �A�C�e�����ʌ��� = 1L << 48,

        /// <summary>
        /// �ő�HP�ቺ�F�ő�HP�l���ꎞ�I�Ɍ�������
        /// </summary>
        �ő�HP�ቺ = 1L << 47,

        // === �o�t�i���̌��ʁj===
        // �񕜁E�����n (0x0000_8000_0000_0000 �`)

        /// <summary>
        /// ���W�F�l�F�p���I��HP���񕜂���
        /// </summary>
        ���W�F�l = 1L << 31,

        /// <summary>
        /// MP���W�F�l�F�p���I��MP���񕜂���
        /// </summary>
        MP���W�F�l = 1L << 30,

        /// <summary>
        /// �����F��_���[�W�ቺ�A�X�^�~�i�E�A�[�}�[�񕜉����A��Ԉُ�~�ό������x����
        /// </summary>
        ���� = 1L << 29,

        /// <summary>
        /// �j���F�^�_���[�W����ƃK�[�h���\�����𓯎��ɓ���
        /// </summary>
        �j�� = 1L << 28,

        /// <summary>
        /// �B���F�w�C�g�����A�ړ����x�����A�������ł̕�������
        /// </summary>
        �B�� = 1L << 27,

        // �X�e�[�^�X�㏸�n (0x0000_4000_0000_0000 �`)

        /// <summary>
        /// �U���͏㏸�F�^����_���[�W����������
        /// </summary>
        �U���͏㏸ = 1L << 26,

        /// <summary>
        /// �h��͏㏸�F�����E���@�h��͂��㏸����
        /// </summary>
        �h��͏㏸ = 1L << 25,

        /// <summary>
        /// �ړ����x�㏸�F�L�����N�^�[�̈ړ����x���㏸����
        /// </summary>
        �ړ����x�㏸ = 1L << 24,

        /// <summary>
        /// ��_���[�W�����F�󂯂�_���[�W����������
        /// </summary>
        ��_���[�W���� = 1L << 23,

        /// <summary>
        /// �^�_���[�W�����F�^����_���[�W����������
        /// </summary>
        �^�_���[�W���� = 1L << 22,

        /// <summary>
        /// �A�N�V���������F�ړ����x�����A��i�W�����v�A�������Ȃǂ̃A�N�V�������\����
        /// </summary>
        �A�N�V�������� = 1L << 21,

        /// <summary>
        /// ����U�������F���@�A�J�E���^�[�A�e�푮���U���Ȃǂ̓���U���̈З͌���
        /// </summary>
        ����U������ = 1L << 20,

        /// <summary>
        /// �A�C�e�����ʑ����F�񕜃A�C�e���⋭���A�C�e���̌��ʂ���������
        /// </summary>
        �A�C�e�����ʑ��� = 1L << 19,

        /// <summary>
        /// �ő�HP�㏸�F�ő�HP�l���ꎞ�I�ɏ㏸����
        /// </summary>
        �ő�HP�㏸ = 1L << 18,

        // ������ʌn (0x0000_2000_0000_0000 �`)

        /// <summary>
        /// �o���A�F���񐔂̍U�������S�ɖ���������h����
        /// </summary>
        �o���A = 1L << 17,

        /// <summary>
        /// �G���`�����g�F����ɑ������ʂ�t�^����i���A���A���A�łȂǁj
        /// </summary>
        �G���`�����g = 1L << 16,

        /// <summary>
        /// �����F���S���Ɏ����I�ɑh��������ʁi���U�I���������j
        /// </summary>
        ���� = 1L << 15,

        /// <summary>
        /// �����ŁF������A�N�V�����������S�ɏ�������
        /// </summary>
        ������ = 1L << 14,

        /// <summary>
        /// ���@�֎~�F���@��X�L���̎g�p���֎~����
        /// </summary>
        ���@�֎~ = 1L << 13,

        // === �������ʌn ===
        // �񕜌n (0x0000_0000_8000_0000 �`)

        /// <summary>
        /// HP�񕜁F������HP���񕜂���
        /// </summary>
        HP�� = 1L << 7,

        /// <summary>
        /// MP�񕜁F������MP���񕜂���
        /// </summary>
        MP�� = 1L << 6,

        /// <summary>
        /// �X�^�~�i�񕜁F�X�^�~�i�𑦍��ɉ񕜂���
        /// </summary>
        �X�^�~�i�� = 1L << 5,

        /// <summary>
        /// �A�[�}�[�񕜁F�A�[�}�[�l���񕜂���
        /// </summary>
        �A�[�}�[�� = 1L << 4,

        // �����n (0x0000_0000_4000_0000 �`)

        /// <summary>
        /// ��Ԉُ�����F�ŁA�����Ȃǂ̏�Ԉُ����������
        /// </summary>
        ��Ԉُ���� = 1L << 3,

        /// <summary>
        /// �o�t�폜�F���ׂẴo�t���ʂ��폜����
        /// </summary>
        �o�t�폜 = 1L << 2,

        /// <summary>
        /// �S���ʉ����F�o�t�E�f�o�t��킸�S�Ă̌��ʂ���������
        /// </summary>
        �S���ʉ��� = 1L << 1,

        // === �J�e�S���}�X�N ===

        /// <summary>
        /// �f�o�t����p�̃r�b�g�}�X�N
        /// </summary>
        �f�o�t�}�X�N = unchecked((long)0xC000_0000_0000_0000),

        /// <summary>
        /// �o�t����p�̃r�b�g�}�X�N
        /// </summary>
        �o�t�}�X�N = 0x3FFF_FFFF_FFFF_FFFF,

        /// <summary>
        /// ��Ԉُ픻��p�̃r�b�g�}�X�N
        /// </summary>
        ��Ԉُ�}�X�N = unchecked((long)0x8000_0000_0000_0000),

        /// <summary>
        /// �X�e�[�^�X�n���ʔ���p�̃r�b�g�}�X�N
        /// </summary>
        �X�e�[�^�X�n�}�X�N = 0x7FFF_0000_0000_0000,

        /// <summary>
        /// �������ʔ���p�̃r�b�g�}�X�N
        /// </summary>
        �������ʃ}�X�N = 0x0000_0000_0000_00FF,

        /// <summary>
        /// �d�ˊ|���\�ȃG�t�F�N�g����p�̃r�b�g�}�X�N�i���l�n�G�t�F�N�g�j
        /// </summary>
        �d�ˊ|���\�}�X�N = 0x3FFF_0000_0000_0000
    }

    /// <summary>
    /// �G�t�F�N�g�̒l�̎�ނ��`����񋓌^
    /// </summary>
    public enum EffectValueType : byte
    {
        /// <summary>
        /// �t���O�^�F�u�[���l�iON/OFF�j�ŊǗ��������ʁi�B���A���قȂǁj
        /// </summary>
        �t���O,

        /// <summary>
        /// ���Z�^�F��l�ɐ��l�����Z������ʁi�U����+50�Ȃǁj
        /// </summary>
        ���Z,

        /// <summary>
        /// ��Z�^�F��l�ɔ{������Z������ʁi�U���́~1.5�Ȃǁj
        /// </summary>
        ��Z,

        /// <summary>
        /// �Œ�l�^�F�񕜗ʂȂǁA�Œ�̐��l���g�p�������
        /// </summary>
        �Œ�l
    }

    /// <summary>
    /// �G�t�F�N�g�̏I���������`����񋓌^
    /// </summary>
    public enum EndConditionType : byte
    {
        /// <summary>
        /// �����F���ʂ𔭊���������ɏI���i�񕜁A�������ʂȂǁj
        /// </summary>
        ����,

        /// <summary>
        /// ���ԁF�w�肳�ꂽ���Ԃ��o�߂���ƏI��
        /// </summary>
        ����,

        /// <summary>
        /// �g�p�񐔁F�w�肳�ꂽ�񐔎g�p�����ƏI���i�U��3��܂ŗL���Ȃǁj
        /// </summary>
        �g�p��,

        /// <summary>
        /// �i���F���������⎀�S�܂Ōp���������
        /// </summary>
        �i��,

        /// <summary>
        /// �����t���F����̏��������������ƏI���������
        /// </summary>
        �����t��
    }

    /// <summary>
    /// �G�t�F�N�g�̐ݒ�f�[�^���i�[����\����
    /// </summary>
    [Serializable]
    public struct EffectData
    {
        /// <summary>
        /// �G�t�F�N�g�̎��
        /// </summary>
        public EffectType type;

        /// <summary>
        /// �G�t�F�N�g�̒l�̎�ށi�t���O�A���Z�A��Z�A�Œ�l�j
        /// </summary>
        public EffectValueType valueType;

        /// <summary>
        /// �G�t�F�N�g�̌��ʗʁi�{���A���Z�l�A�񕜗ʂȂǗp�r�ɂ��قȂ�j
        /// </summary>
        public float value;

        /// <summary>
        /// �G�t�F�N�g�̏I������
        /// </summary>
        public EndConditionType endCondition;

        /// <summary>
        /// �������ԁi�b�j�܂��͎g�p�񐔁iendCondition�ɂ��Ӗ����ς��j
        /// </summary>
        public float duration;

        /// <summary>
        /// �\���D��x�i�����قǗD��I�ɕ\�������j
        /// </summary>
        public int priority;

        /// <summary>
        /// �G�t�F�N�g�f�[�^�̃R���X�g���N�^
        /// </summary>
        /// <param name="type">�G�t�F�N�g�̎��</param>
        /// <param name="valueType">�l�̎��</param>
        /// <param name="value">���ʗ�</param>
        /// <param name="endCondition">�I������</param>
        /// <param name="duration">�������Ԃ܂��͎g�p��</param>
        /// <param name="priority">�\���D��x</param>
        public EffectData(EffectType type, EffectValueType valueType, float value,
                         EndConditionType endCondition = EndConditionType.����,
                         float duration = 10f, int priority = 0)
        {
            this.type = type;
            this.valueType = valueType;
            this.value = value;
            this.endCondition = endCondition;
            this.duration = duration;
            this.priority = priority;
        }

        /// <summary>
        /// �t���O�^�G�t�F�N�g�p�֗̕��ȃR���X�g���N�^
        /// </summary>
        /// <param name="type">�G�t�F�N�g�̎��</param>
        /// <param name="endCondition">�I������</param>
        /// <param name="duration">��������</param>
        /// <param name="priority">�\���D��x</param>
        /// <returns>�t���O�^�̃G�t�F�N�g�f�[�^</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData CreateFlag(EffectType type, EndConditionType endCondition = EndConditionType.����,
                                          float duration = 10f, int priority = 0)
        {
            return new EffectData(type, EffectValueType.�t���O, 1f, endCondition, duration, priority);
        }

        /// <summary>
        /// ���l�^�G�t�F�N�g�p�֗̕��ȃR���X�g���N�^
        /// </summary>
        /// <param name="type">�G�t�F�N�g�̎��</param>
        /// <param name="valueType">�l�̎��</param>
        /// <param name="value">���ʗ�</param>
        /// <param name="endCondition">�I������</param>
        /// <param name="duration">��������</param>
        /// <param name="priority">�\���D��x</param>
        /// <returns>���l�^�̃G�t�F�N�g�f�[�^</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData CreateValue(EffectType type, EffectValueType valueType, float value,
                                           EndConditionType endCondition = EndConditionType.����,
                                           float duration = 10f, int priority = 0)
        {
            return new EffectData(type, valueType, value, endCondition, duration, priority);
        }
    }

    /// <summary>
    /// �A�N�e�B�u�ȃG�t�F�N�g�̃C���X�^���X���Ǘ�����N���X
    /// </summary>
    public class ActiveEffect
    {
        /// <summary>
        /// �G�t�F�N�g�̐ݒ�f�[�^
        /// </summary>
        public EffectData data;

        /// <summary>
        /// �G�t�F�N�g���J�n���ꂽ����
        /// </summary>
        public float startTime;

        /// <summary>
        /// �c�莝�����ԁi�b�j
        /// </summary>
        public float remainingDuration;

        /// <summary>
        /// �c��g�p��
        /// </summary>
        public int remainingUses;

        /// <summary>
        /// ���̃G�t�F�N�g���A�N�e�B�u��Ԃ��ǂ���
        /// </summary>
        public bool isActive;

        /// <summary>
        /// �G�t�F�N�g�̈�ӎ��ʎq�i�d�ˊ|���Ǘ��p�j
        /// </summary>
        public int effectId;

        /// <summary>
        /// �ÓI�J�E���^�i�G�t�F�N�gID�����p�j
        /// �C���X�^���X�����̓x�ɑ����Ă����B
        /// </summary>
        private static int _nextEffectId = 1;

        /// <summary>
        /// �A�N�e�B�u�G�t�F�N�g�̃R���X�g���N�^
        /// </summary>
        /// <param name="data">�G�t�F�N�g�f�[�^</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActiveEffect(EffectData data)
        {
            this.data = data;
            this.startTime = Time.time;
            this.remainingDuration = data.duration;
            this.remainingUses = (int)data.duration;
            this.isActive = true;
            this.effectId = _nextEffectId++;
        }

        /// <summary>
        /// ���̃G�t�F�N�g�������؂ꂩ�ǂ����𔻒肷��
        /// </summary>
        /// <returns>�����؂�̏ꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsExpired()
        {
            switch ( data.endCondition )
            {
                case EndConditionType.����:
                    return remainingDuration <= 0;
                case EndConditionType.�g�p��:
                    return remainingUses <= 0;
                case EndConditionType.����:
                    return true;
                case EndConditionType.�i��:
                    return false;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// �L�����N�^�[�̎Q�Ɨp�C���^�[�t�F�[�X
    /// EffectSystem���L�����N�^�[�Ɍ��ʂ̕ύX��ʒm���邽�߂Ɏg�p
    /// </summary>
    public interface IMyCharacter
    {
        /// <summary>
        /// �G�t�F�N�g���ǉ����ꂽ�ۂɌĂяo����郁�\�b�h
        /// �L�����N�^�[�͂��̃��\�b�h���Œǉ����ꂽ�G�t�F�N�g�ɉ������������s��
        /// </summary>
        /// <param name="effectType">�ǉ����ꂽ�G�t�F�N�g�̃^�C�v</param>
        void EffectTurnOn(EffectType effectType);

        /// <summary>
        /// �G�t�F�N�g���폜���ꂽ�ۂɌĂяo����郁�\�b�h
        /// �L�����N�^�[�͂��̃��\�b�h���ō폜���ꂽ�G�t�F�N�g�ɉ������������s��
        /// </summary>
        /// <param name="effectType">�폜���ꂽ�G�t�F�N�g�̃^�C�v</param>
        void EffectTurnOff(EffectType effectType);
    }

    /// <summary>
    /// �V���v���Ŏg���₷���G�t�F�N�g�V�X�e��
    /// �o�t�E�f�o�t�E��Ԉُ�E������ʂ𓝍��Ǘ�����
    /// �r�b�g�t���O�ɂ�鍂�����݃`�F�b�N�ƁAList�ɂ������I�ȃG�t�F�N�g�Ǘ����s��
    /// </summary>
    public class EffectSystem : MonoBehaviour
    {
        [Header("Settings")]

        /// <summary>
        /// �G�t�F�N�g�̍X�V�Ԋu�i�b�j
        /// �������قǐ��������������ׂ�����
        /// </summary>
        [SerializeField] private float _updateInterval = 0.1f;

        /// <summary>
        /// �f�o�b�O���O���o�͂��邩�ǂ���
        /// </summary>
        [SerializeField] private bool _debugMode = false;

        /// <summary>
        /// �L�����N�^�[�ւ̎Q�Ɓi�G�t�F�N�g�ύX���̒ʒm�p�j
        /// </summary>
        private IMyCharacter _myCharacter;

        /// <summary>
        /// ���݃A�N�e�B�u�ȃG�t�F�N�g�^�C�v�̃r�b�g�t���O
        /// �����ȑ��݃`�F�b�N�p�iO(1)�ł� HasEffect �����j
        /// </summary>
        private long _activeEffectFlags = 0L;

        /// <summary>
        /// �S�ẴA�N�e�B�u�G�t�F�N�g���Ǘ����郊�X�g
        /// �d�ˊ|���\�G�t�F�N�g�͓����^�C�v�ł������ێ������
        /// </summary>
        private List<ActiveEffect> _activeEffects = new List<ActiveEffect>();

        // �C�x���g

        /// <summary>
        /// �G�t�F�N�g���ǉ����ꂽ���ɔ�������C�x���g
        /// </summary>
        public event Action<EffectType> OnEffectAdded;

        /// <summary>
        /// �G�t�F�N�g���������ꂽ���ɔ�������C�x���g
        /// </summary>
        public event Action<EffectType> OnEffectRemoved;

        /// <summary>
        /// �G�t�F�N�g�̒l���ύX���ꂽ���ɔ�������C�x���g
        /// </summary>
        public event Action<EffectType, float> OnEffectValueChanged;

        /// <summary>
        /// ����������
        /// </summary>
        private void Start()
        {
            // MyCharacter�R���|�[�l���g���擾
            _myCharacter = GetComponent<IMyCharacter>();
            if ( _myCharacter == null )
            {
                Debug.LogError("MyCharacter�R���|�[�l���g��������܂���BIMyCharacter���������Ă��������B");
            }

            // ����X�V�J�n
            UpdateEffectsLoop().Forget();
        }

        #region Public Interface

        /// <summary>
        /// �G�t�F�N�g��ǉ�����
        /// �����̓��^�C�v�G�t�F�N�g�͐V�������̂ŏ㏑�������i�d�ˊ|���p�~�j
        /// </summary>
        /// <param name="effectData">�ǉ�����G�t�F�N�g�̃f�[�^</param>
        public void AddEffect(EffectData effectData)
        {
            // �����Ɍ��ʂ𔭊�����^�C�v�̏���
            if ( effectData.endCondition == EndConditionType.���� )
            {
                ApplyInstantEffect(effectData);
                return;
            }

            // �����̓��^�C�v�G�t�F�N�g���폜�i�㏑���̂��߁j
            bool hadExistingEffect = RemoveEffectInternal(effectData.type);

            // �V�����G�t�F�N�g��ǉ�
            var newEffect = new ActiveEffect(effectData);
            _activeEffects.Add(newEffect);

            // �r�b�g�t���O���X�V
            _activeEffectFlags |= (long)effectData.type;

            // �L�����N�^�[�Ɍ��ʒǉ���ʒm
            if ( _myCharacter != null )
            {
                _myCharacter.EffectTurnOn(effectData.type);
            }

            OnEffectAdded?.Invoke(effectData.type);

            if ( _debugMode )
                Debug.Log($"�G�t�F�N�g�ǉ�: {effectData.type} (�l: {effectData.value}, ��������: {effectData.duration}, ID: {newEffect.effectId}, �㏑��: {hadExistingEffect})");
        }

        /// <summary>
        /// ����̃G�t�F�N�g�^�C�v��S�ď�������
        /// </summary>
        /// <param name="type">��������G�t�F�N�g�̃^�C�v</param>
        public void RemoveEffect(EffectType type)
        {
            var removedEffects = new List<EffectType>();

            // �t���Ń��[�v���Ĉ��S�ɍ폜
            for ( int i = _activeEffects.Count - 1; i >= 0; i-- )
            {
                if ( _activeEffects[i].data.type == type )
                {
                    removedEffects.Add(_activeEffects[i].data.type);
                    _activeEffects.RemoveAt(i);
                }
            }

            if ( removedEffects.Count > 0 )
            {
                // �r�b�g�t���O����Y���^�C�v���폜
                _activeEffectFlags &= ~(long)type;

                // �L�����N�^�[�Ɍ��ʏI����ʒm�i�폜���ꂽ�G�t�F�N�g��1���j
                if ( _myCharacter != null )
                {
                    foreach ( var removedType in removedEffects )
                    {
                        _myCharacter.EffectTurnOff(removedType);
                    }
                }

                OnEffectRemoved?.Invoke(type);

                if ( _debugMode )
                    Debug.Log($"�G�t�F�N�g����: {type} ({removedEffects.Count}��)");
            }
        }

        /// <summary>
        /// �����I�ɃG�t�F�N�g���폜����i�ʒm�Ȃ��j
        /// AddEffect�ł̏㏑�������Ŏg�p
        /// </summary>
        /// <param name="type">�폜����G�t�F�N�g�̃^�C�v</param>
        /// <returns>�폜���ꂽ�G�t�F�N�g���������ꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool RemoveEffectInternal(EffectType type)
        {
            bool removed = false;

            // �t���Ń��[�v���Ĉ��S�ɍ폜
            for ( int i = _activeEffects.Count - 1; i >= 0; i-- )
            {
                if ( _activeEffects[i].data.type == type )
                {
                    _activeEffects.RemoveAt(i);
                    removed = true;
                }
            }

            // �r�b�g�t���O����Y���^�C�v���폜
            if ( removed )
            {
                _activeEffectFlags &= ~(long)type;
            }

            return removed;
        }

        /// <summary>
        /// ����̃G�t�F�N�gID�̃G�t�F�N�g����������i�d�ˊ|���\�G�t�F�N�g�p�j
        /// </summary>
        /// <param name="effectId">��������G�t�F�N�g��ID</param>
        public void RemoveEffectById(int effectId)
        {
            for ( int i = _activeEffects.Count - 1; i >= 0; i-- )
            {
                if ( _activeEffects[i].effectId == effectId )
                {
                    var removedType = _activeEffects[i].data.type;
                    _activeEffects.RemoveAt(i);

                    // �r�b�g�t���O����Y���^�C�v���폜
                    _activeEffectFlags &= ~(long)removedType;

                    // �L�����N�^�[�Ɍ��ʏI����ʒm
                    if ( _myCharacter != null )
                    {
                        _myCharacter.EffectTurnOff(removedType);
                    }

                    OnEffectRemoved?.Invoke(removedType);

                    if ( _debugMode )
                        Debug.Log($"�G�t�F�N�g����: {removedType} (ID: {effectId})");

                    break;
                }
            }
        }

        /// <summary>
        /// �w�肳�ꂽ�J�e�S���ɑ�����G�t�F�N�g��S�ď�������
        /// </summary>
        /// <param name="categoryMask">�J�e�S�����w�肷��r�b�g�}�X�N</param>
        public void RemoveEffectsByCategory(EffectType categoryMask)
        {
            var removedEffects = new List<EffectType>();
            var removedTypes = new HashSet<EffectType>();
            long removedBits = 0L;

            // �t���Ń��[�v���Ĉ��S�ɍ폜
            for ( int i = _activeEffects.Count - 1; i >= 0; i-- )
            {
                if ( (_activeEffects[i].data.type & categoryMask) != 0 )
                {
                    var effectType = _activeEffects[i].data.type;
                    removedEffects.Add(effectType);
                    removedTypes.Add(effectType);
                    removedBits |= (long)effectType;
                    _activeEffects.RemoveAt(i);
                }
            }

            if ( removedEffects.Count > 0 )
            {
                // �폜���ꂽ�^�C�v�̃r�b�g���܂Ƃ߂č폜
                _activeEffectFlags &= ~removedBits;

                // �L�����N�^�[�Ɍ��ʏI����ʒm�i�폜���ꂽ�G�t�F�N�g��1���j
                if ( _myCharacter != null )
                {
                    foreach ( var removedEffect in removedEffects )
                    {
                        _myCharacter.EffectTurnOff(removedEffect);
                    }
                }

                // �������ꂽ�^�C�v���ƂɃC�x���g����
                foreach ( var type in removedTypes )
                {
                    OnEffectRemoved?.Invoke(type);
                }

                if ( _debugMode )
                    Debug.Log($"�J�e�S���G�t�F�N�g����: 0x{(long)categoryMask:X} ({removedEffects.Count}��)");
            }
        }

        /// <summary>
        /// �S�Ẵf�o�t����������
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAllDebuffs()
        {
            RemoveEffectsByCategory(EffectType.�f�o�t�}�X�N);
        }

        /// <summary>
        /// �S�Ẵo�t����������
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAllBuffs()
        {
            RemoveEffectsByCategory(EffectType.�o�t�}�X�N);
        }

        /// <summary>
        /// �w�肳�ꂽ�G�t�F�N�g�����݂��邩���`�F�b�N����iO(1) �̍����`�F�b�N�j
        /// </summary>
        /// <param name="type">�`�F�b�N����G�t�F�N�g�̃^�C�v</param>
        /// <returns>�G�t�F�N�g�����݂���ꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasEffect(EffectType type)
        {
            return (_activeEffectFlags & (long)type) != 0;
        }

        /// <summary>
        /// �t���O�^�G�t�F�N�g�̏�Ԃ��擾����
        /// </summary>
        /// <param name="type">�擾����G�t�F�N�g�̃^�C�v</param>
        /// <returns>�t���O�^�G�t�F�N�g���A�N�e�B�u�ȏꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetEffectFlag(EffectType type)
        {
            // �܂��r�b�g�t���O�ō����`�F�b�N
            if ( (_activeEffectFlags & (long)type) == 0 )
                return false;

            // �ڍ׃`�F�b�N�F�t���O�^���A�N�e�B�u�ȃG�t�F�N�g�����邩
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.data.valueType == EffectValueType.�t���O && effect.isActive )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// �G�t�F�N�g�̒l���擾����i�d�ˊ|���p�~�ɂ�蓯�^�C�v��1�̂݁j
        /// </summary>
        /// <param name="type">�擾����G�t�F�N�g�̃^�C�v</param>
        /// <param name="defaultValue">�G�t�F�N�g�����݂��Ȃ��ꍇ�̃f�t�H���g�l</param>
        /// <returns>�G�t�F�N�g�̒l�i���݂��Ȃ��ꍇ�̓f�t�H���g�l�j</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetEffectValue(EffectType type, float defaultValue = 0f)
        {
            // �܂��r�b�g�t���O�ō����`�F�b�N
            if ( (_activeEffectFlags & (long)type) == 0 )
                return defaultValue;

            // �Y������G�t�F�N�g�������i�d�ˊ|���p�~�ɂ��1�̂݁j
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.isActive )
                {
                    if ( effect.data.valueType == EffectValueType.�t���O )
                    {
                        return 1f; // �t���O�^�̓A�N�e�B�u�Ȃ�1
                    }

                    return effect.data.value;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// �X�e�[�^�X�v�Z�p�F�w��^�C�v�̌��ʂ���l�ɓK�p���Čv�Z����i�d�ˊ|���p�~�ɂ��1�̂݁j
        /// </summary>
        /// <param name="effectType">�v�Z�Ɏg�p����G�t�F�N�g�̃^�C�v</param>
        /// <param name="baseValue">��l</param>
        /// <returns>�G�t�F�N�g��K�p�������ʂ̒l</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateStatModifier(EffectType effectType, float baseValue)
        {
            // �܂��r�b�g�t���O�ō����`�F�b�N
            if ( (_activeEffectFlags & (long)effectType) == 0 )
                return baseValue;

            // �Y������G�t�F�N�g�������i�d�ˊ|���p�~�ɂ��1�̂݁j
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == effectType && effect.isActive )
                {
                    switch ( effect.data.valueType )
                    {
                        case EffectValueType.���Z:
                            return baseValue + effect.data.value;
                        case EffectValueType.��Z:
                            return baseValue * effect.data.value;
                        case EffectValueType.�Œ�l:
                            return effect.data.value;
                        case EffectValueType.�t���O:
                            return effect.isActive ? baseValue : 0f;
                    }
                }
            }

            return baseValue;
        }

        /// <summary>
        /// �����̃G�t�F�N�g�^�C�v�ŃX�e�[�^�X���v�Z����
        /// ���Z���ʂ��ɓK�p���A���̌��Z���ʂ�K�p����i�d�ˊ|���p�~�ɂ��e�^�C�v1�̂݁j
        /// </summary>
        /// <param name="baseValue">��l�B�X�e�[�^�X���甲��</param>
        /// <param name="effectTypes">�K�p����G�t�F�N�g�^�C�v�̔z��</param>
        /// <returns>�S�ẴG�t�F�N�g��K�p�������ʂ̒l</returns>
        public float CalculateStatWithMultipleEffects(float baseValue, params EffectType[] effectTypes)
        {
            float result = baseValue;

            // ���Z���ʂ��ɓK�p
            for ( int j = 0; j < effectTypes.Length; j++ )
            {
                var effectType = effectTypes[j];
                if ( (_activeEffectFlags & (long)effectType) != 0 )
                {
                    for ( int i = 0; i < _activeEffects.Count; i++ )
                    {
                        var effect = _activeEffects[i];
                        if ( effect.data.type == effectType && effect.isActive && effect.data.valueType == EffectValueType.���Z )
                        {
                            result += effect.data.value;
                            break; // �d�ˊ|���p�~�ɂ��1�̂�
                        }
                    }
                }
            }

            // ��Z���ʂ���ɓK�p
            for ( int j = 0; j < effectTypes.Length; j++ )
            {
                var effectType = effectTypes[j];
                if ( (_activeEffectFlags & (long)effectType) != 0 )
                {
                    for ( int i = 0; i < _activeEffects.Count; i++ )
                    {
                        var effect = _activeEffects[i];
                        if ( effect.data.type == effectType && effect.isActive && effect.data.valueType == EffectValueType.��Z )
                        {
                            result *= effect.data.value;
                            break; // �d�ˊ|���p�~�ɂ��1�̂�
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// �A�N�e�B�u�ȃG�t�F�N�g�̈ꗗ���擾����iUI�\���p�j
        /// </summary>
        /// <param name="sortByPriority">�D��x���Ƀ\�[�g���邩�ǂ���</param>
        /// <returns>�A�N�e�B�u�ȃG�t�F�N�g�̃��X�g</returns>
        public List<ActiveEffect> GetActiveEffects(bool sortByPriority = true)
        {
            var effects = new List<ActiveEffect>();

            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                if ( _activeEffects[i].isActive )
                {
                    effects.Add(_activeEffects[i]);
                }
            }

            if ( sortByPriority )
            {
                effects.Sort((a, b) => b.data.priority.CompareTo(a.data.priority));
            }

            return effects;
        }

        /// <summary>
        /// �G�t�F�N�g�̎g�p�񐔂������i�d�ˊ|���p�~�ɂ��1�̂݁j
        /// �g�p�񐔐����̂���G�t�F�N�g�Ŏg�p�����
        /// </summary>
        /// <param name="type">�g�p�񐔂������G�t�F�N�g�̃^�C�v</param>
        /// <param name="amount">������</param>
        public void ConsumeEffectUse(EffectType type, int amount = 1)
        {
            // �܂��r�b�g�t���O�ō����`�F�b�N
            if ( (_activeEffectFlags & (long)type) == 0 )
                return;

            // �Y������G�t�F�N�g�������i�d�ˊ|���p�~�ɂ��1�̂݁j
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.data.endCondition == EndConditionType.�g�p�� )
                {
                    effect.remainingUses -= amount;
                    if ( effect.remainingUses <= 0 )
                    {
                        RemoveEffectById(effect.effectId);
                    }
                    break; // �d�ˊ|���p�~�ɂ��1�̂�
                }
            }
        }

        /// <summary>
        /// �G�t�F�N�g�����݂��A�g�p�񐔐���������ꍇ�Ɏg�p�񐔂������
        /// ���݃`�F�b�N�Ǝg�p�񐔏���𓯎��ɍs�������I�ȃ��\�b�h�i�d�ˊ|���p�~�ɂ��1�̂݁j
        /// </summary>
        /// <param name="type">�����G�t�F�N�g�̃^�C�v</param>
        /// <param name="amount">������</param>
        /// <returns>�G�t�F�N�g�����݂��A�g�p�񐔂�������ꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeEffect(EffectType type, int amount = 1)
        {
            // �܂��r�b�g�t���O�ō����`�F�b�N
            if ( (_activeEffectFlags & (long)type) == 0 )
                return false;

            // �Y������G�t�F�N�g�������i�d�ˊ|���p�~�ɂ��1�̂݁j
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.data.endCondition == EndConditionType.�g�p�� )
                {
                    effect.remainingUses -= amount;

                    if ( effect.remainingUses <= 0 )
                    {
                        RemoveEffectById(effect.effectId);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// �G�t�F�N�g�����݂��A�g�p�񐔐���������ꍇ�̂�true��Ԃ��i����͂��Ȃ��j
        /// �g�p�O�̎��O�`�F�b�N�p�i�d�ˊ|���p�~�ɂ��1�̂݁j
        /// </summary>
        /// <param name="type">�`�F�b�N����G�t�F�N�g�̃^�C�v</param>
        /// <returns>�G�t�F�N�g�����݂��A�g�p�񐔐���������ꍇtrue</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasConsumableEffect(EffectType type)
        {
            // �܂��r�b�g�t���O�ō����`�F�b�N
            if ( (_activeEffectFlags & (long)type) == 0 )
                return false;

            // �Y������G�t�F�N�g�������i�d�ˊ|���p�~�ɂ��1�̂݁j
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.data.endCondition == EndConditionType.�g�p�� )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// �g�p�񐔐����G�t�F�N�g�̎c��g�p�񐔂��擾����i�d�ˊ|���p�~�ɂ��1�̂݁j
        /// </summary>
        /// <param name="type">�`�F�b�N����G�t�F�N�g�̃^�C�v</param>
        /// <returns>�c��g�p�񐔁i�G�t�F�N�g�����݂��Ȃ��ꍇ��0�j</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRemainingUses(EffectType type)
        {
            // �܂��r�b�g�t���O�ō����`�F�b�N
            if ( (_activeEffectFlags & (long)type) == 0 )
                return 0;

            // �Y������G�t�F�N�g������
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( effect.data.type == type && effect.data.endCondition == EndConditionType.�g�p�� )
                {
                    return effect.remainingUses;
                }
            }

            return 0;
        }

        #endregion

        #region Update & Management

        /// <summary>
        /// �G�t�F�N�g�̒���X�V���s���񓯊����[�v
        /// ���Ԍo�߂ɂ��G�t�F�N�g�̊Ǘ����s��
        /// </summary>
        /// <returns>UniTask</returns>
        private async UniTaskVoid UpdateEffectsLoop()
        {
            while ( this != null && gameObject.activeInHierarchy )
            {
                UpdateEffects();
                await UniTask.Delay(TimeSpan.FromSeconds(_updateInterval));
            }
        }

        /// <summary>
        /// �G�t�F�N�g�̍X�V����
        /// �������Ԃ̌����Ɗ����؂�G�t�F�N�g�̏������s��
        /// </summary>
        private void UpdateEffects()
        {
            // stackalloc�ŏ����ȃo�b�t�@�������m�ہi�q�[�v���蓖�ĂȂ��j
            Span<int> expiredEffectIds = stackalloc int[32]; // �ʏ�͏\���ȗe��
            int expiredCount = 0;

            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];

                // ���Ԍo�ߏ���
                if ( effect.data.endCondition == EndConditionType.���� )
                {
                    effect.remainingDuration -= _updateInterval;
                }

                // �����؂�`�F�b�N
                if ( effect.IsExpired() )
                {
                    if ( expiredCount < expiredEffectIds.Length )
                    {
                        expiredEffectIds[expiredCount++] = effect.effectId;
                    }
                    else
                    {
                        // �o�b�t�@�����t�ɂȂ����烋�[�v���I��
                        // �c��̃G�t�F�N�g�͎��t���[���ŏ���
                        if ( _debugMode )
                            Debug.Log($"�G�t�F�N�g�o�b�t�@���t�B{expiredCount}���폜��A�c��͎��t���[���ŏ���");
                        break;
                    }
                }
            }

            // �����؂�G�t�F�N�g�������iSpan�𒼐ڎg�p�j
            for ( int i = 0; i < expiredCount; i++ )
            {
                RemoveEffectById(expiredEffectIds[i]);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// �����Ɍ��ʂ𔭊�����G�t�F�N�g�̏���
        /// HP�񕜁A�������ʂȂǂ̑������ʂ����s����
        /// </summary>
        /// <param name="effectData">���s����G�t�F�N�g�̃f�[�^</param>
        private void ApplyInstantEffect(EffectData effectData)
        {
            switch ( effectData.type )
            {
                case EffectType.HP��:
                    // HP�񕜏���
                    if ( _debugMode )
                        Debug.Log($"HP��: {effectData.value}");
                    break;

                case EffectType.MP��:
                    // MP�񕜏���
                    if ( _debugMode )
                        Debug.Log($"MP��: {effectData.value}");
                    break;

                case EffectType.��Ԉُ����:
                    RemoveEffectsByCategory(EffectType.��Ԉُ�}�X�N);
                    break;

                case EffectType.�o�t�폜:
                    RemoveAllBuffs();
                    break;

                case EffectType.�S���ʉ���:
                    // �S���ʉ����̏ꍇ�A�폜�����S�G�t�F�N�g���L�^���Čʒʒm
                    var allEffectsToRemove = new List<EffectType>();
                    for ( int i = 0; i < _activeEffects.Count; i++ )
                    {
                        allEffectsToRemove.Add(_activeEffects[i].data.type);
                    }

                    _activeEffects.Clear();
                    _activeEffectFlags = 0L;

                    // �폜���ꂽ�G�t�F�N�g��1���ʒm
                    if ( _myCharacter != null )
                    {
                        foreach ( var removedType in allEffectsToRemove )
                        {
                            _myCharacter.EffectTurnOff(removedType);
                        }
                    }
                    return; // �������^�[���ŉ���myCharacter�Ăяo�����X�L�b�v
            }

            if ( _debugMode )
                Debug.Log($"�������ʓK�p: {effectData.type} (�l: {effectData.value})");
        }

        #endregion

        #region Debug & Utility

        /// <summary>
        /// �f�o�b�O�p�F���݃A�N�e�B�u�ȃG�t�F�N�g��\������
        /// </summary>
        [ContextMenu("�f�o�b�O - �A�N�e�B�u�G�t�F�N�g�\��")]
        private void DebugShowActiveEffects()
        {
            Debug.Log("=== �A�N�e�B�u�G�t�F�N�g ===");
            Debug.Log($"�A�N�e�B�u�t���O: 0x{_activeEffectFlags:X16}");
            Debug.Log($"�G�t�F�N�g��: {_activeEffects.Count}");

            var effectGroups = new Dictionary<EffectType, List<ActiveEffect>>();

            // �^�C�v�ʂɃO���[�v��
            for ( int i = 0; i < _activeEffects.Count; i++ )
            {
                var effect = _activeEffects[i];
                if ( !effectGroups.ContainsKey(effect.data.type) )
                {
                    effectGroups[effect.data.type] = new List<ActiveEffect>();
                }
                effectGroups[effect.data.type].Add(effect);
            }

            // �O���[�v�ʂɕ\��
            foreach ( var group in effectGroups )
            {
                if ( group.Value.Count == 1 )
                {
                    var effect = group.Value[0];
                    Debug.Log($"{effect.data.type}: �l={effect.data.value}, �c�莞��={effect.remainingDuration:F1}�b, ID={effect.effectId}");
                }
                else
                {
                    Debug.Log($"{group.Key}: {group.Value.Count}�̃G�t�F�N�g�i�d�ˊ|���j");
                    foreach ( var effect in group.Value )
                    {
                        Debug.Log($"  �l={effect.data.value}, �c�莞��={effect.remainingDuration:F1}�b, ID={effect.effectId}");
                    }
                }
            }
        }

        /// <summary>
        /// �f�o�b�O�p�F�e�X�g�p�̃o�t��ǉ�����
        /// </summary>
        [ContextMenu("�f�o�b�O - �e�X�g�o�t�ǉ�")]
        private void DebugAddTestBuff()
        {
            AddEffect(EffectPresets.�U���͋���(1.5f, 30f));
        }

        /// <summary>
        /// �f�o�b�O�p�F�e�X�g�p�̃f�o�t��ǉ�����
        /// </summary>
        [ContextMenu("�f�o�b�O - �e�X�g�f�o�t�ǉ�")]
        private void DebugAddTestDebuff()
        {
            AddEffect(EffectPresets.�Ō���(5f, 15f));
        }

        /// <summary>
        /// �f�o�b�O�p�F�����̍U���̓o�t�������ǉ�����i�㏑���e�X�g�j
        /// </summary>
        [ContextMenu("�f�o�b�O - �U���̓o�t�㏑���e�X�g")]
        private void DebugAddMultipleAttackBuffs()
        {
            // �d�ˊ|���p�~�ɂ��A�e�G�t�F�N�g�͑O�̂��̂��㏑������
            AddEffect(EffectPresets.�U���͋���(1.2f, 60f));
            Debug.Log("�U����1.2�{�i60�b�j��ǉ�");

            AddEffect(EffectPresets.�U���͋���(1.5f, 30f));
            Debug.Log("�U����1.5�{�i30�b�j�ŏ㏑��");

            AddEffect(EffectPresets.�U���͋���(2.0f, 15f));
            Debug.Log("�U����2.0�{�i15�b�j�ŏ㏑��");
        }

        /// <summary>
        /// �f�o�b�O�p�F�g�p�񐔐����G�t�F�N�g��ǉ�����
        /// </summary>
        [ContextMenu("�f�o�b�O - �g�p�񐔐����G�t�F�N�g�ǉ�")]
        private void DebugAddConsumableEffect()
        {
            var consumableEffect = EffectData.CreateValue(
                EffectType.����U������,
                EffectValueType.��Z,
                2.0f,
                EndConditionType.�g�p��,
                5f, // 5��g�p�\
                6
            );
            AddEffect(consumableEffect);
        }

        /// <summary>
        /// �f�o�b�O�p�FTryConsumeEffect�̃e�X�g
        /// </summary>
        [ContextMenu("�f�o�b�O - TryConsumeEffect�e�X�g")]
        private void DebugTryConsumeEffect()
        {
            bool consumed = TryConsumeEffect(EffectType.����U������);
            Debug.Log($"�G�t�F�N�g����: {consumed}");

            int remaining = GetRemainingUses(EffectType.����U������);
            Debug.Log($"�c��g�p��: {remaining}");
        }

        /// <summary>
        /// �f�o�b�O�p�FEffectTurnOn��EffectTurnOff�̃e�X�g
        /// </summary>
        [ContextMenu("�f�o�b�O - �G�t�F�N�g�ʒm�e�X�g")]
        private void DebugEffectNotificationTest()
        {
            // �e�X�g�p�G�t�F�N�g��ǉ�
            AddEffect(EffectPresets.�U���͋���(1.5f, 10f));
            AddEffect(EffectPresets.�Ō���(5f, 15f));

            // �����҂��Ă���f�o�t���폜
            StartCoroutine(DebugRemoveDebuffsDelayed());
        }

        private System.Collections.IEnumerator DebugRemoveDebuffsDelayed()
        {
            yield return new WaitForSeconds(2f);
            Debug.Log("=== �f�o�t�ꊇ�폜�e�X�g ===");
            RemoveAllDebuffs();
        }

        /// <summary>
        /// �f�o�b�O�p�FHasEffect�̐��\�e�X�g
        /// </summary>
        [ContextMenu("�f�o�b�O - HasEffect���\�e�X�g")]
        private void DebugHasEffectPerformanceTest()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 100�����HasEffect�`�F�b�N
            for ( int i = 0; i < 1000000; i++ )
            {
                bool hasAttackBuff = HasEffect(EffectType.�U���͏㏸);
                bool hasPoison = HasEffect(EffectType.��);
                bool hasSilence = HasEffect(EffectType.����);
            }

            stopwatch.Stop();
            Debug.Log($"HasEffect 100������s����: {stopwatch.ElapsedMilliseconds}ms");
        }

        #endregion
    }

    #region Helper Classes

    /// <summary>
    /// �悭�g�p�����G�t�F�N�g�f�[�^�̃v���Z�b�g�W
    /// ��^�I�ȃG�t�F�N�g���ȒP�ɍ쐬���邽�߂̃w���p�[�N���X
    /// </summary>
    public static class EffectPresets
    {
        // === �o�t�n�v���Z�b�g ===

        /// <summary>
        /// �U���͋����G�t�F�N�g���쐬����
        /// </summary>
        /// <param name="multiplier">�U���͂̔{��</param>
        /// <param name="duration">�������ԁi�b�j</param>
        /// <returns>�U���͋����G�t�F�N�g</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData �U���͋���(float multiplier, float duration) =>
            EffectData.CreateValue(EffectType.�U���͏㏸, EffectValueType.��Z, multiplier, EndConditionType.����, duration, 3);

        /// <summary>
        /// �h��͋����G�t�F�N�g���쐬����
        /// </summary>
        /// <param name="multiplier">�h��͂̔{��</param>
        /// <param name="duration">�������ԁi�b�j</param>
        /// <returns>�h��͋����G�t�F�N�g</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData �h��͋���(float multiplier, float duration) =>
            EffectData.CreateValue(EffectType.�h��͏㏸, EffectValueType.��Z, multiplier, EndConditionType.����, duration, 3);

        /// <summary>
        /// �ړ����x�����G�t�F�N�g���쐬����
        /// </summary>
        /// <param name="multiplier">�ړ����x�̔{��</param>
        /// <param name="duration">�������ԁi�b�j</param>
        /// <returns>�ړ����x�����G�t�F�N�g</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData �ړ����x����(float multiplier, float duration) =>
            EffectData.CreateValue(EffectType.�ړ����x�㏸, EffectValueType.��Z, multiplier, EndConditionType.����, duration, 2);

        /// <summary>
        /// �B�����ʂ��쐬����i�t���O�^�j
        /// </summary>
        /// <param name="duration">�������ԁi�b�j</param>
        /// <returns>�B���G�t�F�N�g</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData �B������(float duration) =>
            EffectData.CreateFlag(EffectType.�B��, EndConditionType.����, duration, 4);

        // === �f�o�t�n�v���Z�b�g ===

        /// <summary>
        /// �Ō��ʂ��쐬����
        /// </summary>
        /// <param name="damagePerSecond">1�b������̃_���[�W��</param>
        /// <param name="duration">�������ԁi�b�j</param>
        /// <returns>�ŃG�t�F�N�g</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData �Ō���(float damagePerSecond, float duration) =>
            EffectData.CreateValue(EffectType.��, EffectValueType.�Œ�l, damagePerSecond, EndConditionType.����, duration, 8);

        /// <summary>
        /// ������ʂ��쐬����
        /// </summary>
        /// <param name="multiplier">�e��X�e�[�^�X�̔{��</param>
        /// <param name="duration">�������ԁi�b�j</param>
        /// <returns>����G�t�F�N�g</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData �������(float multiplier, float duration) =>
            EffectData.CreateValue(EffectType.����, EffectValueType.��Z, multiplier, EndConditionType.����, duration, 6);

        /// <summary>
        /// ���ٌ��ʂ��쐬����i�t���O�^�j
        /// </summary>
        /// <param name="duration">�������ԁi�b�j</param>
        /// <returns>���كG�t�F�N�g</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData ���ٌ���(float duration) =>
            EffectData.CreateFlag(EffectType.����, EndConditionType.����, duration, 7);

        // === �������ʌn�v���Z�b�g ===

        /// <summary>
        /// HP�񕜌��ʂ��쐬����
        /// </summary>
        /// <param name="amount">�񕜗�</param>
        /// <returns>HP�񕜃G�t�F�N�g</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData HP��(float amount) =>
            EffectData.CreateValue(EffectType.HP��, EffectValueType.�Œ�l, amount, EndConditionType.����, 0f, 0);

        /// <summary>
        /// MP�񕜌��ʂ��쐬����
        /// </summary>
        /// <param name="amount">�񕜗�</param>
        /// <returns>MP�񕜃G�t�F�N�g</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData MP��(float amount) =>
            EffectData.CreateValue(EffectType.MP��, EffectValueType.�Œ�l, amount, EndConditionType.����, 0f, 0);

        /// <summary>
        /// ��Ԉُ�S�������ʂ��쐬����
        /// </summary>
        /// <returns>��Ԉُ�����G�t�F�N�g</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EffectData ��Ԉُ�S����() =>
            EffectData.CreateFlag(EffectType.��Ԉُ����, EndConditionType.����, 0f, 0);
    }

    #endregion
}