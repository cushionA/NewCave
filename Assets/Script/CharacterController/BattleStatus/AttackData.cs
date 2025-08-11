using CharacterController;
using MoreMountains.CorgiEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CharacterController.StatusData.BrainStatus;


/// <summary>
/// 構造体にする
/// 構造体なら値を代入できるから可読性上がる
/// 
/// 魔法や攻撃アクションなどの情報のべースになるもの
/// </summary>
public class AttackData : ActionData
{
    #region 定義

    #region enum

    /// <summary>
    /// 行動ごとに入れ替える必要のあるデータ
    /// とりわけダメージ計算に使うものの構造体
    /// 状態異常もどうにかしないとね
    /// 
    /// Health側では接触時に送られてきたのがダメージステータスか、回復ステータスかで動作を変えるようにしよ
    /// </summary>
    public class DamageStatus
    {

        [Header("攻撃のメイン属性")]
        public Element mainElement;//g

        /// <summary>
        /// ダメージ計算で使う
        /// </summary>
        [Header("攻撃の全属性")]
        public Element useElement;//g

        /// <summary>
        /// モーション値
        /// </summary>
        [Header("モーション値")]
        public float mValue;//g

        /// <summary>
        /// アーマー削り
        /// </summary>
        [Header("アーマー削り")]
        public int shock;//g

        /// <summary>
        /// 吹き飛ばす力
        /// これが0以上なら吹き飛ばし攻撃を行う
        /// </summary>
        [Header("吹き飛ばす力")]
        public Vector2 blowPower;//g

        /// <summary>
        /// パリィのアーマー削りに対する抵抗
        /// </summary>
        [Header("パリィのアーマー削りに対する抵抗")]
        public float parryResist;

        /// <summary>
        /// ヒット回数制限
        /// </summary>
        [Header("ヒット回数制限")]
        public byte hitLimit;//g

        /// <summary>
        /// 攻撃自体の性質
        /// </summary>
        public AttackFeature attackFeature;
    }

    /// <summary>
    /// 攻撃モーションの性質
    /// </summary>
    [Flags]
    public enum AttackMoveFeature : byte
    {
        軽い攻撃 = 1 << 0,// 弾かれる
        重い攻撃 = 1 << 1,
        前方ガード攻撃 = 1 << 2,// ガード攻撃。アニメイベントでトリガーされる。
        後方ガード攻撃 = 1 << 3,
        落下攻撃 = 1 << 4,
        運搬可能 = 1 << 5,// 敵を押す攻撃
        なし = 0
    }


    /// <summary>
    /// 攻撃判定の性質
    /// 攻撃そのものが持つ、ダメージ計算時に影響する性質
    /// </summary>
    [Flags]
    public enum AttackFeature : byte
    {
        パリィ不可 = 1 << 0,
        ヒット時HPドレイン = 1 << 1,//命中時自分が回復
        ヒット時MPドレイン = 1 << 2,//命中時自分がMP回復。
        なし = 0
    }

    /// <summary>
    /// アクションの強度
    /// エフェクトの判断に使う
    /// </summary>
    public enum AttackLevel : byte
    {
        弱攻撃,
        通常攻撃,
        強攻撃,
        必殺,
        特殊エフェクト,
        射出モーション//属性別弾丸使用モーション
    }

    #endregion

    #region クラス

    #endregion クラス

    #endregion

    #region モーション再生関連

    /// <summary>
    /// どんなエフェクトや音をもらうか
    /// </summary>
    [Header("攻撃エフェクトのレベル")]
    public AttackLevel effectLevel;

    /// <summary>
    /// パリィされるか、軽いか重いかなど
    /// </summary>
    [Header("攻撃の性質")]
    public AttackMoveFeature feature;

    [Header("敵に対して攻撃が持つ効果")]
    /// <summary>
    /// 毒とかね
    /// </summary>
    public EffectData attackMotionEvent;

    #endregion

    /// <summary>
    /// 特定の特徴を含むかをチェック
    /// </summary>
    /// <param name="checkCondition"></param>
    /// <returns></returns>
    public bool CheckFeature(AttackMoveFeature checkCondition)
    {
        return (feature & checkCondition) > 0;
    }
}
