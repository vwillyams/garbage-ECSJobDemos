using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TwoStickClassicExample
{
    public class UpdatePlayerHUD : MonoBehaviour
    {
        private float m_CachedValue;

        private void Update()
        {
            int displayedHealth = 0;

            var player = FindObjectOfType<Player>();
            
            if (player != null)
            {
                displayedHealth = (int) player.GetComponent<Health>().Value;
            }

            if (m_CachedValue != displayedHealth)
            {
                Text t = GetComponent<Text>();
                if (t != null)
                {
                    if (displayedHealth > 0)
                        t.text = $"HEALTH: {displayedHealth}";
                    else
                        t.text = "GAME OVER";
                }

                m_CachedValue = displayedHealth;
            }
        }
    }
}