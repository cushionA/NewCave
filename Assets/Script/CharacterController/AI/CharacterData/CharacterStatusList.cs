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
        /// キャラクターのステータスの一覧。
        /// </summary>
        public BrainStatus[] statusList;

        /// <summary>
        /// 各キャラのモードごとのAIデータの配列を管理する構造体。
        /// 直接は使わず部分配列を取ることで、Jobごとに必要なデータを取得する。
        /// </summary>
        public BrainDataForJob brainArray;

        /// <summary>
        /// Job用に行動データのモードごとのマッピング済み配列を作成。
        /// </summary>
        public void MakeBrainDataArray()
        {
            // キャラクターのステータスから、Job用に行動データの配列を作成する。
            CharacterModeData[][] characterModeData = new CharacterModeData[this.statusList.Length][];

            // 各キャラクターのモード設定を取得
            for ( int i = 0; i < this.statusList.Length; i++ )
            {
                characterModeData[i] = this.statusList[i].characterModeSetting;
            }

            // Job用のBrainDataForJobを作成
            brainArray = new BrainDataForJob(characterModeData);
        }

        [ContextMenu("キャラクター並び替え")]
        private void CharacterSortByID()
        {
            _ = this.statusList.OrderBy(x => x.characterID);
        }

    }
}

