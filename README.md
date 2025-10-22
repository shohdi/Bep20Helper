# 🪙 **Bep20Helper — The Ultimate Blockchain Helper Library!** 🚀  

> A C# library so powerful, even your grandma could deploy smart contracts (okay, maybe not 😅).  
> Built on **Nethereum**, this bad boy lets you **create wallets**, **check balances**, **swap tokens**, and **transfer BNB/WBNB** — all while sipping coffee ☕.

---

## ⚙️ **Installation**

1. Clone this repo (or just grab the `.cs` file, we’re not judging):
   ```bash
   git clone https://github.com/shohdi/Bep20Helper.git
   ```
2. Open your C# project in Visual Studio or Rider.  
3. Add references to:
   ```bash
   dotnet add package Nethereum.Web3
   dotnet add package Nethereum.HdWallet
   dotnet add package Newtonsoft.Json
   ```
4. Include the namespace:
   ```csharp
   using tra.osi;
   ```

That’s it. You’re officially on the blockchain 😎  

---

## 🧠 **How to Use**

First, create an instance of `BlockchainHelper`:
```csharp
var helper = new BlockchainHelper("https://bsc-dataseed.binance.org"); // default RPC
```

Now let’s dive into each magical method 🪄👇  

---

## 🪪 **CreateNewWallet()**

**Creates a new wallet with a mnemonic, private key, and BSC address.**

```csharp
var wallet = helper.CreateNewWallet();
Console.WriteLine($"Mnemonic: {wallet.Mnemonic}");
Console.WriteLine($"Address: {wallet.Address}");
Console.WriteLine($"PrivateKey: {wallet.PrivateKeyHex}");
```

> ⚠️ Store these offline, don’t be the guy crying on Reddit 😭  

---

## 🧩 **RestoreFromMnemonic()**

**Restores a wallet from your mnemonic phrase.**

```csharp
var restored = helper.RestoreFromMnemonic("trend coral mango ...");
Console.WriteLine($"Restored Address: {restored.Address}");
```

> Perfect for those who lost their private key but not their brain 🧠  

---

## 🔑 **FromPrivateKey()**

**Get your wallet address from a raw private key.**

```csharp
var wallet = helper.FromPrivateKey("abc123...yourPrivateKey...");
Console.WriteLine(wallet.Address);
```

> Instant results. No magic spell required ✨  

---

## 💰 **GetBalance()**

**Check your BNB or token balance.**

```csharp
var bnbBalance = await helper.GetBalance("0xYourWalletAddress");
var tokenBalance = await helper.GetBalance("0xYourWalletAddress", "0xTokenAddress");
Console.WriteLine($"BNB: {bnbBalance}, Token: {tokenBalance}");
```

> Checks both native and token balances like a crypto ninja 🥷  

---

## 💸 **Transfer()**

**Send BNB or any ERC20/BEP20 token.**

```csharp
string tx = await helper.Transfer("yourPrivateKey", "0xRecipient", 0.05m); // BNB
Console.WriteLine($"BNB Transfer Tx: {tx}");

string tokenTx = await helper.Transfer("yourPrivateKey", "0xRecipient", 100, "0xTokenAddress");
Console.WriteLine($"Token Transfer Tx: {tokenTx}");
```

> Transfers so smooth, even PancakeSwap gets jealous 🥞  

---

## 💱 **SwapTokenToToken()**

**Swap between tokens using PancakeSwap V2 Router.**

```csharp
string swapTx = await helper.SwapTokenToToken(
    "yourPrivateKey",
    "0xFromToken",
    "0xToToken",
    10m
);
Console.WriteLine($"Swap Tx: {swapTx}");
```

> 🔄 Makes token swaps as easy as flipping pancakes.  

---

## 🌕 **BnbToWbnb()** & **WbnbToBnb()**

**Wrap or unwrap BNB (because PancakeSwap likes WBNB better 🤷).**

```csharp
string wrapTx = await helper.BnbToWbnb("yourPrivateKey", 0.1m);
Console.WriteLine($"BNB → WBNB: {wrapTx}");

string unwrapTx = await helper.WbnbToBnb("yourPrivateKey", 0.1m);
Console.WriteLine($"WBNB → BNB: {unwrapTx}");
```

> Like turning your BNB into a burrito 🌯 and back again.  

---

## 📈 **GetPrice()**

**Get the current price of a token (in USD).**

```csharp
decimal bnbPrice = await helper.GetPrice(); // Default is WBNB
decimal tokenPrice = await helper.GetPrice("0xTokenAddress");
Console.WriteLine($"BNB: ${bnbPrice}, Token: ${tokenPrice}");
```

> Now you can cry in real-time watching your bags drop 📉😭  

---

## 🧪 **Running a Project**

To use this in your C# console project:

1. Create a new project:
   ```bash
   dotnet new console -n MyCryptoApp
   cd MyCryptoApp
   ```
2. Add Nethereum dependencies (see installation above).  
3. Add `BlockchainHelper.cs` to your project.  
4. Write your code in `Program.cs`:
   ```csharp
   using tra.osi;
   using System.Threading.Tasks;

   class Program
   {
       static async Task Main(string[] args)
       {
           var helper = new BlockchainHelper();
           var wallet = helper.CreateNewWallet();
           Console.WriteLine($"Your new wallet: {wallet.Address}");

           var balance = await helper.GetBalance(wallet.Address);
           Console.WriteLine($"BNB Balance: {balance}");
       }
   }
   ```
5. Run it 🚀
   ```bash
   dotnet run
   ```

> Boom 💥 You’re officially a blockchain developer (send screenshots to your mom).  

---

## 🧑‍💻 **Supported Networks**

- ✅ Binance Smart Chain (Mainnet)
- 🧪 Testnet (change RPC to `https://data-seed-prebsc-1-s1.binance.org:8545/`)

---

## ⚡ **Pro Tips**

- Use **async/await** or your app will hang like a Windows 98 PC 🖥️  
- Always use a **small test amount** before going full degen 💀  
- Never commit your **private key** to GitHub — unless you enjoy losing money 🕳️  

---

## ❤️ **Contributing**

Found a bug? Broke your wallet? Just want to say hi?  
Open an issue or PR — memes accepted as payment 🐸  

---

## 🥳 **License**

MIT License — use it, fork it, break it, meme it.  

## ** if you like our work , you can donate to us on bep20 address  
```
0x62237e5B246B9326716Fd2c2a97be5705F422aFD
```

## ** you can also support us by swapping bnb to osiris token **
```
0x9Ca5EeaCF3517F2304244e82226Ae01D410290f2

https://www.osiristoken.com/
```