using Binance.Net.Clients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotBot
{
    static class BinanceExtensions
    {
        public async static Task<Decimal> GetBalance(this BinanceRestClient client, Currency currency)
        {
            var balanceData = await client.SpotApi.Account.GetBalancesAsync(currency.GetSymbol());
            if (!balanceData.Success)
            {
                Console.WriteLine(balanceData.Error.Message);
            }
            else
            {
                foreach (var asset in balanceData.Data)
                {
                    Console.WriteLine(string.Format("Asset: {0} | Balance: {1}", asset.Asset, asset.Available));
                    // more info available in obj asset, just pick the ones you need
                    return asset.Available;
                }
            }

            return new decimal(0);
        }

    }
}
