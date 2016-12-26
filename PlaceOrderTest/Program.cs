using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using RealTick.Api.Application;
using RealTick.Api.Domain;
using RealTick.Api.Domain.Order;
using RealTick.Api.ClientAdapter;

namespace PlaceOrderTest
{
    class Program
    {
        // the sample code expects the symbol to be in a variable or constant called "_symbol"  
        private const string _symbol = "AAPL";
        private const string route = "DEMO";   // For production this should be NSDQ
        private const string exchange = "NAS";
        private const int volume = 25;
        private static string buyOrSell = "Buy";  // Buy for Buy and Sell for Sell

        static void Main(string[] args)
        {
            Console.WriteLine("Buying Stock");
            using (var app = new ClientAdapterToolkitApp())
            {
                Run(app);
            }
            Console.WriteLine("Hit Enter to quit");
            var name = Console.ReadLine();
            Console.WriteLine("Bye Mike");
        }

        static void WriteLine(string fmt, params object[] args)
        {
            Console.WriteLine(fmt, args);
        }

        // the sample code uses a method called "WaitAny" -- we can map this to WaitHandle.WaitOne()  
        static bool WaitAny(int timeout, WaitHandle handle)
        {
            handle.WaitOne(timeout);
            return true;
        }

        private enum State { WaitingForConnect, OrderInPlay, OrderDone, ConnectionFailed };
        static State _state = State.WaitingForConnect;
        static readonly AutoResetEvent _event = new AutoResetEvent(false);

        static void Run(ToolkitApp app)
        {
            using (var cache = new OrderCache(app))
            {
                cache.OnLive += CacheOnOnLive;
                cache.OnDead += CacheOnOnDead;
                cache.OnOrder += CacheOnOnOrder;

                _state = State.WaitingForConnect;
                cache.Start();

                while (_state != State.OrderDone && _state != State.ConnectionFailed)
                {
                    if (!WaitAny(10000, _event))
                    {
                        WriteLine("TIMED OUT WAITING FOR RESPONSE");
                        break;
                    }
                }

            } // end using cache
            WriteLine("DONE");
        }

        // one or more events have been received from the EMS
        static void CacheOnOnOrder(object sender, DataEventArgs<OrderRecord> dataEventArgs)
        {
            // NOTE: this logic assumes that only one order is in flight.  If you have multiple active orders, then you can distinguish 
            // among them by examining the OrderTag, which corresponds to the value from the OrderBuilder used to send the order.
            foreach (var ord in dataEventArgs)
            {
                //Removed because DisplayOrder is not showing up for this context
                //DisplayOrder(ord);
                WriteLine("Type: {0} Status: {1}",ord.Type, ord.CurrentStatus);
                if (ord.Type == "UserSubmitOrder")
                    if (ord.CurrentStatus == "COMPLETED" || ord.CurrentStatus == "DELETED")
                        _state = State.OrderDone;

                if (ord.Type == "ExchangeTradeOrder")
                    WriteLine("GOT FILL FOR {0} {1} AT {2}", ord.Buyorsell, ord.Volume, ord.Price);
                if (ord.Type == "ExchangeKillOrder")
                    WriteLine("GOT KILL");
            }
            _event.Set();
        }

        // connection to the EMS lost
        static void CacheOnOnDead(object sender, EventArgs eventArgs)
        {
            WriteLine("CONNECTION FAILED");
            _state = State.ConnectionFailed;
            _event.Set();
        }

        // connection to the EMS established, and account data received
        static void CacheOnOnLive(object sender, EventArgs eventArgs)
        {
            var cache = sender as OrderCache;

            Trace.Assert(cache != null);

            WriteLine("SUBMITTING ORDER");

            // We send a market order, to maximize the chance that we will
            // successfully get a fill as desired for this example.
            var bld = new OrderBuilder(cache);
            //bld.SetAccount(null, "TEST", null, null);
            /////////////////////////////////////////////
            //  This is where the account settings are //
            /////////////////////////////////////////////
            bld.SetAccount("LATEST", "TEST", "01", "CATALYST");  // Dev version
            //bld.SetAccount("LSPS", "80", "LSPS", "1LD41223");  // Prod version
            if (buyOrSell == "Buy")
            {
                bld.SetBuySell(OrderBuilder.BuySell.BUY);
            } else if(buyOrSell == "Sell")
            {
                bld.SetBuySell(OrderBuilder.BuySell.SELL);
            }
            bld.SetExpiration(OrderBuilder.Expiration.DAY);
            bld.SetRoute(route);
            bld.SetSymbol(_symbol, exchange, OrderBuilder.SecurityType.STOCK);
            bld.SetPriceMarket();
            bld.SetVolume(volume);
            cache.SubmitOrder(bld);

            _state = State.OrderInPlay;
            _event.Set();
        }
    }
}
