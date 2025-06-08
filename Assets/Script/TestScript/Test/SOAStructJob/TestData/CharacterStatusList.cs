using Sirenix.Utilities;
using System.Linq;
using TestScript.SOATest;
using Unity.Collections;
using UnityEngine;
using static TestScript.SOATest.SOAStatus;

[CreateAssetMenu(fileName = "CharacterStatusList", menuName = "Scriptable Objects/CharacterStatusList")]
public class CharacterStatusList : ScriptableObject
{
    /// <summary>
    /// �L�����N�^�[�̃X�e�[�^�X�̈ꗗ�B
    /// </summary>
    public SOAStatus[] statusList;

    /// <summary>
    /// 
    /// </summary>
    public NativeArray<BrainDataForJob> brainArray;

    /// <summary>
    /// 
    /// </summary>
    public NativeArray<HateSettingForJob> hateSetting;

    /// <summary>
    /// Job�p�ɍs���f�[�^�̔z����쐬�B
    /// </summary>
    public void MakeBrainDataArray()
    {
        brainArray = new NativeArray<BrainDataForJob>(statusList.Length, allocator: Allocator.Persistent);
        hateSetting = new NativeArray<HateSettingForJob>(statusList.Length, allocator: Allocator.Persistent);

        for ( int i = 0; i < statusList.Length; i++ )
        {
            brainArray[i] = new BrainDataForJob(statusList[i].brainData, statusList[i].judgeInterval, statusList[i].moveJudgeInterval);
            hateSetting[i] = new HateSettingForJob(statusList[i].hateCondition);
        }
    }

    [ContextMenu("�L�����N�^�[���ёւ�")]
    private void CharacterSortByID()
    {
        statusList.OrderBy(x => x.characterID);
    }

}
