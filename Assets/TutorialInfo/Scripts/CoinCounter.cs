using UnityEngine;

public class CoinCounter : MonoBehaviour
{
    public int CoinCount { get; private set; }

    public static CoinCounter EnsureExists()
    {
        CoinCounter counter = FindObjectOfType<CoinCounter>();
        if (counter != null)
        {
            return counter;
        }

        return new GameObject(nameof(CoinCounter)).AddComponent<CoinCounter>();
    }

    public void AddCoin()
    {
        CoinCount++;
        Debug.Log($"Coins collected: {CoinCount}", this);
    }
}