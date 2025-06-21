using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Odinで使えるシリアライズ可能なディクショナリのベーススクリプト。
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public abstract class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [SerializeField, HideInInspector]
    private List<TKey> _keyData = new();

    [SerializeField, HideInInspector]
    private List<TValue> _valueData = new();

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        this.Clear();
        for ( int i = 0; i < this._keyData.Count && i < this._valueData.Count; i++ )
        {
            this[this._keyData[i]] = this._valueData[i];
        }
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
        this._keyData.Clear();
        this._valueData.Clear();

        foreach ( KeyValuePair<TKey, TValue> item in this )
        {
            this._keyData.Add(item.Key);
            this._valueData.Add(item.Value);
        }
    }
}