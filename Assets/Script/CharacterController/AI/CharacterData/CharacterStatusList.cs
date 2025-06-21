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
        /// 
        /// </summary>
        public NativeArray<BrainDataForJob> brainArray;

        /// <summary>
        /// 
        /// </summary>
        public NativeArray<HateSettingForJob> hateSetting;

        /// <summary>
        /// Job用に行動データの配列を作成。
        /// </summary>
        public void MakeBrainDataArray()
        {
            this.brainArray = new NativeArray<BrainDataForJob>(this.statusList.Length, allocator: Allocator.Persistent);
            this.hateSetting = new NativeArray<HateSettingForJob>(this.statusList.Length, allocator: Allocator.Persistent);

            for ( int i = 0; i < this.statusList.Length; i++ )
            {
                this.brainArray[i] = new BrainDataForJob(this.statusList[i].brainData, this.statusList[i].judgeInterval, this.statusList[i].moveJudgeInterval);
                this.hateSetting[i] = new HateSettingForJob(this.statusList[i].hateCondition);
            }
        }

        [ContextMenu("キャラクター並び替え")]
        private void CharacterSortByID()
        {
            _ = this.statusList.OrderBy(x => x.characterID);
        }

    }
}

