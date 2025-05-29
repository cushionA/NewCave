using System;
using System.Runtime.InteropServices;
using UnityEngine;
using static CharacterController.BrainStatus;

namespace CharacterController
{
    /// <summary>
    /// �L�����N�^�[�R���g���[���[�ɓ�����N���X�B<br/>
    /// ���̃N���X�����̂̓L�����N�^�[�̕s�ρA���Q�ƌ^�𒆐S�ɂ����X�e�[�^�X�f�[�^�����B<br/>
    /// AI��Job���o�͂����󋵔��f���ʃf�[�^���󂯎���āA���̒ʂ�ɓ����B<br/>
    /// �����̃f�[�^��ScriptableObject����擾���邽�߁A�����I�ɂ͂��̃N���X���̂����f�[�^�͔��f���ʂ����B<br/>
    /// ���f�̉ߒ��͉B�����A�؂藣������ōs���ɕK�v�ȃf�[�^���������B
    /// </summary>
    public class BaseController : MonoBehaviour
    {
        #region ��`

        #region enum��`

        /// <summary>
        /// ���f���ʂ��܂Ƃ߂Ċi�[����r�b�g���Z�p
        /// </summary>
        [Flags]
        public enum JudgeResult
        {
            �����Ȃ� = 0,
            �V�������f������ = 1 << 1,// ���̎��͈ړ��������ς���
            �����]�������� = 1 << 2,
            ��Ԃ�ύX���� = 1 << 3,
        }

        /// <summary>
        /// �L�����̓���s�����L�^���邽�߂̃t���O�񋓌^�B
        /// ����Ȃ����B�Ή�����s����������̃`�[���w�C�g���グ���肷��΂��������B
        /// </summary>
        public enum CharacterActionLog
        {
            �񕜂���,
            �񕜂��ꂽ,
            ��_���[�W���󂯂�,
            �����ɑ�_���[�W��^����,

        }

        #endregion enum��`

        #region �\���̒�`

        /// <summary>
        /// �s���Ɏg�p����f�[�^�̍\���́B
        /// ���݂̍s����ԁA�ړ������A���f��A�ȂǕK�v�Ȃ��̂͑S�Ď��߂�B
        /// ����ɏ]���ē����Ƃ����f�[�^�B
        /// 
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct MovementInfo
        {

            // ����L�������ǂ��������A�݂����ȃf�[�^�܂œ���Ă�
            // �V�K���f��̓^�[�Q�b�g����ւ��Ƃ��A���f���ԓ���ւ��Ƃ�������ƃL�����f�[�^��������B

            /// <summary>
            /// �^�[�Q�b�g�̃n�b�V���R�[�h
            /// ����ő�����擾����B
            /// �����ւ̎x�����[�u�̃^�[�Q�b�g�����肤�邱�Ƃ͓��ɓ����B
            /// </summary>
            public int targetHash;

            /// <summary>
            /// ���݂̃^�[�Q�b�g�Ƃ̋����B�}�C�i�X������̂ŕ����ł�����B
            /// </summary>
            public int targetDirection;

            /// <summary>
            /// �ԍ��ōs�����w�肷��B
            /// �U���Ɍ��炸�����Ƃ����S���B�ړ���������g�p���[�V�����A�s���̎�ʂ܂Łi���@�Ƃ��ړ��Ƃ��j
            /// �������̍\���f�[�^�̓X�e�[�^�X�Ɏ������Ƃ��B�s����Ԃ��Ƃɔԍ��Ŏw�肳�ꂽ�s��������B
            /// </summary>
            public int actNum;

            /// <summary>
            /// ���f���ʂɂ��Ă̏����i�[����r�b�g
            /// </summary>
            public JudgeResult result;

            /// <summary>
            /// ���݂̍s����ԁB
            /// </summary>
            public ActState moveState;

        }

        #endregion

        #endregion

        /// �e�X�g�Ŏg�p����X�e�[�^�X�B<br></br>
        /// ���f�Ԋu�̃f�[�^�������Ă���B<br></br>
        /// �C���X�y�N�^����ݒ�B
        /// </summary>
        [SerializeField]
        protected BrainStatus status;

        /// <summary>
        /// �ړ��Ɏg�p���镨���R���|�[�l���g�B
        /// </summary>
        [SerializeField]
        private Rigidbody2D rb;

        /// <summary>
        /// ���񔻒f�������𐔂���B<br></br>
        /// �񓯊��Ɠ����ŁA���҂��锻�f�񐔂Ƃ̊Ԃ̌덷���قȂ邩������B<br></br>
        /// �ŏ��̍s���̕�����1�����������l�ɁB
        /// </summary>
        [HideInInspector]
        public long judgeCount = -1;

        /// <summary>
        /// �Q�[���I�u�W�F�N�g�̃n�b�V���l
        /// </summary>
        public int objecthash;

        /// <summary>
        /// �����������B
        /// </summary>
        protected void Initialize()
        {
            // �V�����L�����f�[�^�𑗂�A�R���o�b�g�}�l�[�W���[�ɑ���B
            // ����A����ς�ޗ������Č������ō���Ă��炨���B
            // NativeContainer�܂ލ\���̂��R�s�[����̂Ȃ񂩂��킢�B
            // ���������R�s�[���Ă��A�������ō�������̓��[�J���ϐ��ł����Ȃ�����Dispose()����̖��͂Ȃ��͂��B
            AIManager.instance.CharacterAdd(this.status, this.gameObject);
        }

        /// <summary>
        /// �s���𔻒f���郁�\�b�h�B
        /// </summary>
        protected void MoveJudgeAct()
        {
            // 50%�̊m���ō��E�ړ��̕������ς��B
            // moveDirection = (UnityEngine.Random.Range(0, 100) >= 50) ? 1 : -1;

            //  rb.linearVelocityX = moveDirection * status.xSpeed;

            //Debug.Log($"���l�F{moveDirection * status.xSpeed} ���x�F{rb.linearVelocityX}");

            //lastJudge = GameManager.instance.NowTime;
            this.judgeCount++;
        }

        /// <summary>
        /// �^�[�Q�b�g�����߂čU������B
        /// </summary>
        protected void Attackct()
        {

        }

        /// <summary>
        /// �߂���͈͒T�����ēG�����擾���鏈���̂ЂȌ^
        /// �����Ŕ͈͓��Ɏ擾�����L������10�̂܂Ńo�b�t�@���ċ������Ń\�[�g
        /// �ЂƂ܂�������
        /// </summary>
        public void NearSearch()
        {
            unsafe
            {
                // �X�^�b�N�Ƀo�b�t�@���m�ہi10�̂܂Łj
                Span<RaycastHit2D> results = stackalloc RaycastHit2D[10];

                //int hitCount = Physics.SphereCastNonAlloc(
                //    AIManager.instance.charaDataDictionary[objecthash].liveData.nowPosition,
                //    20,
                //    results,
                //    0
                //);

            }

        }

        /// <summary>
        /// �e�X�g�f�[�^�쐬�p�̃��\�b�h�B
        /// ���j�b�g�e�X�g�ŃR���|�[�l���g����L�����N�^�[�f�[�^����邽�߂ɕK�v�B
        /// ���ۂ̃Q�[���ł͎g��Ȃ��B
        /// </summary>
        /// <returns></returns>
        public (BrainStatus, GameObject) MakeTestData()
        {
            return (this.status, this.gameObject);
        }

    }
}


