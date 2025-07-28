using System.Linq;
using Unity.Collections;
using UnityEngine;
using static CharacterController.StatusData.BrainStatus;

namespace CharacterController.StatusData
{
    [CreateAssetMenu(fileName = "CharacterStatusList", menuName = "Scriptable Objects/CharacterStatusList")]
    public class CharacterStatusList : ScriptableObject
    {
        /// <summary>
        /// �L�����N�^�[�̃X�e�[�^�X�̈ꗗ�B
        /// </summary>
        public BrainStatus[] statusList;

        /// <summary>
        /// �e�L�����̃��[�h���Ƃ�AI�f�[�^�̔z����Ǘ�����\���́B
        /// ���ڂ͎g�킸�����z�����邱�ƂŁAJob���ƂɕK�v�ȃf�[�^���擾����B
        /// </summary>
        public BrainDataForJob brainArray;

        /// <summary>
        /// Job�p�ɍs���f�[�^�̃��[�h���Ƃ̃}�b�s���O�ςݔz����쐬�B
        /// </summary>
        public void MakeBrainDataArray()
        {
            // �L�����N�^�[�̃X�e�[�^�X����AJob�p�ɍs���f�[�^�̔z����쐬����B
            CharacterModeData[][] characterModeData = new CharacterModeData[this.statusList.Length][];

            // �e�L�����N�^�[�̃��[�h�ݒ���擾
            for ( int i = 0; i < this.statusList.Length; i++ )
            {
                characterModeData[i] = this.statusList[i].characterModeSetting;
            }

            // Job�p��BrainDataForJob���쐬
            brainArray = new BrainDataForJob(characterModeData);
        }

        [ContextMenu("�L�����N�^�[���ёւ�")]
        private void CharacterSortByID()
        {
            _ = this.statusList.OrderBy(x => x.characterID);
        }

    }
}

