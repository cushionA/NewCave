namespace CharacterController.EventSystem
{
    /// <summary>
    /// �O������̃A�N�V�����ɑ΂��Ĕ������邽�߂̃C�x���g�f�[�^�B
    /// ������ł͏�Ԃ�ύX���邽�߂̃C�x���g�f�[�^���`���܂��B
    /// </summary>
    public struct ReactionEventData
    {
        public enum ReactionType
        {
            �Ȃ� = 0,
            ��e = 1 << 0,
            ��_���[�W = 1 << 1,
            �A����e = 1 << 2,// �͈͍U���Ɋ������܂ꂽ�Ƃ��̌��o�ɂ��g������
            �߂��ɓG������ = 1 << 3,// �Z���T�[�@�\�œG�̐ڋ߂����o
        }

        public ReactionType Type;

    }

    /// <summary>
    /// �O������̖��߂�s���w����s�����߂̃C�x���g�f�[�^�B
    /// ������ł̓f�[�^��ێ����邱�Ƃ͂Ȃ��A���̏�Ŏ󂯓���邩�ǂ��������߂�B
    /// �󂯓��ꂽ��͎w��s���̃f�[�^�Ǝw�背�x���������āA����̃��x���̎w�肪���邩���s�ɂ���Ă��̃f�[�^�������B
    /// ���s�ɂ͎w����s�񐔂�����A�������������܂ł̓f�[�^���c��B
    /// ���邢�͑����s�ł�����̂����ɂ���H
    /// </summary>
    public struct CommandEventData
    {
        public enum CommandType
        {
            None,
            Move,
            Attack,
            Defend,
            UseItem,
            Interact
        }

        public CommandType Type;

    }

}

