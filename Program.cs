using Binance.Net.Clients;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.CommonObjects;
using SpotBot;
using System.Threading.Tasks;
using Telegram.Bot;
using IniParser;
using IniParser.Model;

var parser = new FileIniDataParser();
IniData data = parser.ReadFile("settings.ini");

string channelId = data.Global["TELEGRAM_CHANNEL"].ToString();
var botClient = new TelegramBotClient(data.Global["TELEGRAM_BOT"].ToString());

var me = await botClient.GetMeAsync();

string binanceKey = data.Global["BINANCE_KEY"].ToString();
string binanceSecret = data.Global["BINANCE_SECRET"].ToString();

BinanceRestClient.SetDefaultOptions(options =>
{
    options.ApiCredentials = new ApiCredentials(binanceKey, binanceSecret); // <- Provide you API key/secret in these fields to retrieve data related to your account
});

BinanceSocketClient.SetDefaultOptions(options =>
{
    options.ApiCredentials = new ApiCredentials(binanceKey, binanceSecret);
});

var client = new BinanceRestClient();
var socketClient = new BinanceSocketClient();

decimal spread = 100;
decimal fee = 0.01M;

if (args[0] != "B" && args[0] != "S")
{
    throw new Exception("First argument must define first operation: buy or sell.");
}

var nextOperation = args[0] == "B" ? OperationType.BUY : OperationType.SELL;

decimal balanceUsdt = Currency.USDT.Normalize(await client.GetBalance(Currency.USDT)); //current balance in USDT
decimal lastBalanceUsdt = balanceUsdt; //previous balance is same during start, TODO store previous in a file

decimal balanceBtc = Currency.BTC.Normalize(await client.GetBalance(Currency.BTC)); //current balance in BTC
decimal lastBalanceBtc = balanceBtc; //previous balance is same during start, TODO store previous in a file

int i = 0; //handled operations count (resets every 100)
await botClient.SendTextMessageAsync(channelId, "Started with mode: " + nextOperation.ToString());



var tickerSubscriptionResult = socketClient.SpotApi.ExchangeData.SubscribeToTickerUpdatesAsync("BTCUSDT", async (update) =>
{
    var openOrders = await client.SpotApi.Trading.GetOpenOrdersAsync("BTCUSDT");
    if (openOrders.Data.Count() > 0)
    {
        await botClient.SendTextMessageAsync(channelId, "Skipping, got open orders.");
        return;
    }

    decimal balanceUsdt = Currency.USDT.Normalize(await client.GetBalance(Currency.USDT));
    decimal balanceBtc = Currency.BTC.Normalize(await client.GetBalance(Currency.BTC));
    var currentPrice = update.Data.LastPrice;
    Console.WriteLine(currentPrice.ToString());

    if (nextOperation == OperationType.BUY) //if next operation is BUY and we have money (not in pending)
    {
        var btcValueGros = Currency.BTC.Normalize(balanceUsdt / (currentPrice + spread)); //wallet value in BTC        
        var btcValueNet = Currency.BTC.Normalize(btcValueGros - btcValueGros * fee); //wallet value after we apply fee

        if (btcValueNet > lastBalanceBtc) {//if we can get more BTC than we had previously even after fee        
            //convert our balance to BTC
            var balanceInBtc = Currency.USDT.Normalize(balanceUsdt / currentPrice);
            var order = await client.SpotApi.Trading.PlaceOrderAsync("BTCUSDT", Binance.Net.Enums.OrderSide.Buy, Binance.Net.Enums.SpotOrderType.Market, quoteQuantity: balanceUsdt /*price: currentPrice,*/); //BUY btc for current price
            //var order = await client.SpotApi.Trading.PlaceOrderAsync("BTCUSDT", Binance.Net.Enums.OrderSide.Buy, Binance.Net.Enums.SpotOrderType.Market, quoteQuantity: 1, /*price: currentPrice,*/ timeInForce: Binance.Net.Enums.TimeInForce.GoodTillCanceled);

            if (order.Success)
            {
                await botClient.SendTextMessageAsync(channelId, "Placed a BUY order for: " + balanceUsdt.ToString() + " @ " + currentPrice.ToString());
                lastBalanceBtc = 0; //irrelevant
                lastBalanceUsdt = balanceUsdt; //store how much USDT we had
                nextOperation = OperationType.SELL;
            }
            else
            {
                await botClient.SendTextMessageAsync(channelId, "[ERROR] Failed to BUY order for: " + balanceUsdt.ToString() + " @ " + currentPrice.ToString() + " reason: " + order.ToString());
            }
        } else if (i % 50 == 0)
        {
            await botClient.SendTextMessageAsync(channelId, "Running, in mode " + nextOperation.ToString() + " awaiting: " + btcValueNet.ToString() + " to go above " + lastBalanceBtc.ToString());
        }
    }
    else if (nextOperation == OperationType.SELL)
    {
        var profitGross = Currency.USDT.Normalize(balanceBtc * (currentPrice - spread)); //wartosc w USDT
        var profitNet = Currency.USDT.Normalize(profitGross - profitGross * fee); //wartosc NETTO w usdt

        if (profitNet > lastBalanceUsdt) //if that gives us more USDT than previously
        {
            //we try to place everything we got
            var order = await client.SpotApi.Trading.PlaceOrderAsync("BTCUSDT", Binance.Net.Enums.OrderSide.Sell, Binance.Net.Enums.SpotOrderType.Market, quantity: balanceBtc /*price: currentPrice,*/); //SELL all BTC for current price
            Console.WriteLine("Placed a SELL order for: " + balanceUsdt.ToString() + " @ " + currentPrice.ToString());
            if (order.Success)
            {
                await botClient.SendTextMessageAsync(channelId, "Placed a SELL order for: " + balanceBtc.ToString() + " @ " + currentPrice.ToString());
                lastBalanceBtc = balanceBtc; //amount btc we had
                lastBalanceUsdt = 0; //irrelevant


                nextOperation = OperationType.BUY;
            }
            else
            {
                await botClient.SendTextMessageAsync(channelId, "[ERROR] Failed to SELL order for: " + balanceUsdt.ToString() + " @ " + currentPrice.ToString() + " reason: " + order.ToString());
            }
        } else if (i % 50 == 0)
        {
            await botClient.SendTextMessageAsync(channelId, "Running, in mode " + nextOperation.ToString() + " awaiting: " + profitNet.ToString() + " to go above: " + lastBalanceUsdt.ToString());
        }
    }

    if (i++ >= 100)
    {        
        i = 0;
    }
});

Thread.Sleep(Timeout.Infinite);
