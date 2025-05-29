using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static CharacterController.BrainStatus;

/// <summary>
/// CharacterDataのステータスをランダム化するユーティリティクラス
/// </summary>
public static class CharacterDataRandomizer
{
    /// <summary>
    /// CharacterDataのUnsafeListをランダム化する
    /// </summary>
    /// <param name="characterList">ランダム化するキャラクターデータのリスト</param>
    /// <param name="seed">乱数シード（オプション）</param>
    public static void RandomizeCharacterData(ref UnsafeList<CharacterData> characterList, int? seed = null)
    {
        // 乱数シードの設定
        if ( seed.HasValue )
        {
            UnityEngine.Random.InitState(seed.Value);
        }
        else
        {
            UnityEngine.Random.InitState((int)DateTime.Now.Ticks);
        }

        // 各キャラクターのステータスをランダム化
        for ( int i = 0; i < characterList.Length; i++ )
        {
            // UnsafeListから要素を取得
            CharacterData data = characterList[i];

            // 個別のCharacterDataをランダム化するメソッドを呼び出す
            RandomizeCharacterData(ref data, characterList);

            // UnsafeListに値を戻す
            characterList[i] = data;
        }
    }

    /// <summary>
    /// 個別のCharacterDataをランダム化する
    /// </summary>
    /// <param name="data">ランダム化するキャラクターデータ</param>
    /// <param name="allCharacters">全キャラクターのリスト（ヘイト設定用）</param>
    public static void RandomizeCharacterData(ref CharacterData data, UnsafeList<CharacterData> allCharacters = default)
    {

        // 基本ステータスをランダム化
        // 標準値の範囲内でランダム値を設定
        data.liveData.maxHp += UnityEngine.Random.Range(100, 1000);
        data.liveData.maxMp += UnityEngine.Random.Range(50, 500);
        data.liveData.dispAtk += UnityEngine.Random.Range(10, 100);
        data.liveData.dispDef += UnityEngine.Random.Range(10, 100);
        data.liveData.atk.slash += UnityEngine.Random.Range(10, 100);
        data.liveData.def.slash += UnityEngine.Random.Range(10, 100);

        // 現在のHP/MPをランダム化（最大値の範囲内）
        data.liveData.currentHp = UnityEngine.Random.Range(1, data.liveData.maxHp + 1);
        data.liveData.currentMp = UnityEngine.Random.Range(0, data.liveData.maxMp + 1);

        // HP/MPの割合を更新
        data.liveData.hpRatio = (int)((float)data.liveData.currentHp / data.liveData.maxHp * 100);
        data.liveData.mpRatio = (int)((float)data.liveData.currentMp / data.liveData.maxMp * 100);

        // 位置をランダム化
        data.liveData.nowPosition = new Vector2(
            UnityEngine.Random.Range(-200f, 200f),
            UnityEngine.Random.Range(-200f, 200f)
        );

        // ターゲットカウントをランダム化
        data.targetingCount = UnityEngine.Random.Range(0, 5);

        // 行動状態をランダム化
        //  data.liveData.actState = (ActState)UnityEngine.Random.Range(0, 5); // ActStateの数に合わせて調整

        // 最後の判断時間をランダム化
        data.lastJudgeTime = UnityEngine.Random.Range(0f, 10f);

        // 個人ヘイトをランダム化（約10%のキャラクターに対してヘイト値を設定）
        if ( data.personalHate.IsCreated && allCharacters.IsCreated )
        {
            data.personalHate.Clear();
            int hateEntries = Mathf.Max(1, allCharacters.Length / 10);
            int selfIndex = -1;

            // 自分自身のインデックスを見つける
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
                if ( targetIndex != selfIndex ) // 自分自身には設定しない
                {
                    int hateValue = UnityEngine.Random.Range(1, 100);
                    _ = data.personalHate.TryAdd(allCharacters[targetIndex].hashCode, hateValue);
                }
            }
        }
    }
}