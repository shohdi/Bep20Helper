using System;

namespace tra.osi
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IBlockchainHelper help = new BlockchainHelper();
            var osirBal = help.GetBalance("0x526B4d1C4F63F379991106d0bD82c402BEd7AeD1", "0x9Ca5EeaCF3517F2304244e82226Ae01D410290f2").GetAwaiter().GetResult();
            var bnb = help.GetBalance("0x526B4d1C4F63F379991106d0bD82c402BEd7AeD1").GetAwaiter().GetResult();
            var usdt = help.GetBalance("0x526B4d1C4F63F379991106d0bD82c402BEd7AeD1", "0x55d398326f99059fF775485246999027B3197955").GetAwaiter().GetResult();
            var wbnb = help.GetBalance("0x526B4d1C4F63F379991106d0bD82c402BEd7AeD1", "0xbb4Cdb9CBd36B01bD1cBaEBF2De08d9173bc095c").GetAwaiter().GetResult();
            Console.WriteLine($"Osir Balance : {osirBal}");
            Console.WriteLine($"bnb Balance : {bnb}");
            Console.WriteLine($"usdt Balance : {usdt}");
            Console.WriteLine($"wbnb Balance : {wbnb}");
            Console.WriteLine($"BNB Price : {help.GetPrice().GetAwaiter().GetResult()}");
            Console.WriteLine($"Osir Price : {help.GetPrice("0x9Ca5EeaCF3517F2304244e82226Ae01D410290f2").GetAwaiter().GetResult()}");
			//swap 1 usdt to wbnb
			//string tx = help.SwapTokenToToken("privateKey", "0x55d398326f99059fF775485246999027B3197955", "0xbb4Cdb9CBd36B01bD1cBaEBF2De08d9173bc095c" , 1m).GetAwaiter().GetResult();
			//Console.WriteLine($"Transaction of swap : {tx}");

            //get 0.5 usd wbnb price
            var usdtBnbPrice = 1 / help.GetPrice().GetAwaiter().GetResult();
            var halfPrice = usdtBnbPrice / 2.0m;

            //withdraw halfprice to bnb
            //tx = help.WbnbToBnb("privateKey",halfPrice).GetAwaiter().GetResult();
            //Console.WriteLine($"withdraw transaction : {tx}");


            //transfer 0.5 to 0x62237e5B246B9326716Fd2c2a97be5705F422aFD
            //string tx = help.Transfer("privateKey", "0x62237e5B246B9326716Fd2c2a97be5705F422aFD", halfPrice).GetAwaiter().GetResult();
            //Console.WriteLine($"Transfer bnb transaction : {tx}");


            //swap 0.5 to osir
            //tx = help.SwapTokenToToken("privateKey", "0xbb4Cdb9CBd36B01bD1cBaEBF2De08d9173bc095c", "0x9Ca5EeaCF3517F2304244e82226Ae01D410290f2", halfPrice).GetAwaiter().GetResult();
            //Console.WriteLine($"Transaction of swap osir : {tx}");


            Console.WriteLine("Press any key to continue!");
			Console.ReadKey();
        }
    }
}
