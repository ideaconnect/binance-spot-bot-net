using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotBot
{
    internal enum Currency
    {
        USDT = 0,
        BTC = 1
    };

    static class CurrencyExtensions
    {
        public static String GetSymbol(this Currency c)
        {
            switch (c)
            {
                case Currency.USDT:
                    return "USDT";
                case Currency.BTC:
                    return "BTC";
                default:
                    return "ERROR";
            }
        }

        public static decimal Normalize(this Currency c, decimal amount)
        {
            switch (c)
            {
                case Currency.USDT:
                    return Math.Floor(amount * 100) / 100;
                case Currency.BTC:
                    return Math.Floor(amount * 100000) / 100000;
                default:
                    return 0;
            }
        }
    }
}
