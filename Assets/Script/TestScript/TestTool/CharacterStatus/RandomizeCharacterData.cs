using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static CharacterController.BrainStatus;

/// <summary>
/// CharacterData�̃X�e�[�^�X�������_�������郆�[�e�B���e�B�N���X
/// </summary>
public static class CharacterDataRandomizer
{
    /// <summary>
    /// CharacterData��UnsafeList�������_��������
    /// </summary>
    /// <param name="characterList">�����_��������L�����N�^�[�f�[�^�̃��X�g</param>
    /// <param name="seed">�����V�[�h�i�I�v�V�����j</param>
    public static void RandomizeCharacterData(ref UnsafeList<CharacterData> characterList, int? seed = null)
    {
        // �����V�[�h�̐ݒ�
        if ( seed.HasValue )
        {
            UnityEngine.Random.InitState(seed.Value);
        }
        else
        {
            UnityEngine.Random.InitState((int)DateTime.Now.Ticks);
        }

        // �e�L�����N�^�[�̃X�e�[�^�X�������_����
        for ( int i = 0; i < characterList.Length; i++ )
        {
            // UnsafeList����v�f���擾
            CharacterData data = characterList[i];

            // �ʂ�CharacterData�������_�������郁�\�b�h���Ăяo��
            RandomizeCharacterData(ref data, characterList);

            // UnsafeList�ɒl��߂�
            characterList[i] = data;
        }
    }

    /// <summary>
    /// �ʂ�CharacterData�������_��������
    /// </summary>
    /// <param name="data">�����_��������L�����N�^�[�f�[�^</param>
    /// <param name="allCharacters">�S�L�����N�^�[�̃��X�g�i�w�C�g�ݒ�p�j</param>
    public static void RandomizeCharacterData(ref CharacterData data, UnsafeList<CharacterData> allCharacters = default)
    {

        // ��{�X�e�[�^�X�������_����
        // �W���l�͈͓̔��Ń����_���l��ݒ�
        data.liveData.maxHp += UnityEngine.Random.Range(100, 1000);
        data.liveData.maxMp += UnityEngine.Random.Range(50, 500);
        data.liveData.dispAtk += UnityEngine.Random.Range(10, 100);
        data.liveData.dispDef += UnityEngine.Random.Range(10, 100);
        data.liveData.atk.slash += UnityEngine.Random.Range(10, 100);
        data.liveData.def.slash += UnityEngine.Random.Range(10, 100);

        // ���݂�HP/MP�������_�����i�ő�l�͈͓̔��j
        data.liveData.currentHp = UnityEngine.Random.Range(1, data.liveData.maxHp + 1);
        data.liveData.currentMp = UnityEngine.Random.Range(0, data.liveData.maxMp + 1);

        // HP/MP�̊������X�V
        data.liveData.hpRatio = (int)((float)data.liveData.currentHp / data.liveData.maxHp * 100);
        data.liveData.mpRatio = (int)((float)data.liveData.currentMp / data.liveData.maxMp * 100);

        // �ʒu�������_����
        data.liveData.nowPosition = new Vector2(
            UnityEngine.Random.Range(-200f, 200f),
            UnityEngine.Random.Range(-200f, 200f)
        );

        // �^�[�Q�b�g�J�E���g�������_����
        data.targetingCount = UnityEngine.Random.Range(0, 5);

        // �s����Ԃ������_����
        //  data.liveData.actState = (ActState)UnityEngine.Random.Range(0, 5); // ActState�̐��ɍ��킹�Ē���

        // �Ō�̔��f���Ԃ������_����
        data.lastJudgeTime = UnityEngine.Random.Range(0f, 10f);

        // �l�w�C�g�������_�����i��10%�̃L�����N�^�[�ɑ΂��ăw�C�g�l��ݒ�j
        if ( data.personalHate.IsCreated && allCharacters.IsCreated )
        {
            data.personalHate.Clear();
            int hateEntries = Mathf.Max(1, allCharacters.Length / 10);
            int selfIndex = -1;

            // �������g�̃C���f�b�N�X��������
            for ( int i = 0; i < allCharacters.Length; i++ )
            {
                if ( allCharacters[i].hashCode == data.hashCode )
                {
                    selfIndex = i;
                    break;
                }
            }

            for ( int j = 0; j < hateEntries; j++ )
            {
                int targetIndex = UnityEngine.Random.Range(0, allCharacters.Length);
                if ( targetIndex != selfIndex ) // �������g�ɂ͐ݒ肵�Ȃ�
                {
                    int hateValue = UnityEngine.Random.Range(1, 100);
                    _ = data.personalHate.TryAdd(allCharacters[targetIndex].hashCode, hateValue);
                }
            }
        }
    }
}