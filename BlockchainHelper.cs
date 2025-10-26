using Nethereum.Signer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Nethereum.HdWallet;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.ABI.CompilationMetadata;
using Nethereum.Web3;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using System.Numerics;
using Nethereum.Web3.Accounts;
using System.Diagnostics.Metrics;
using System.Reflection;
using Nethereum.Contracts.Standards.ERC20.TokenList;
using Newtonsoft.Json.Linq;
using System.IO;
using Nethereum.Hex.HexTypes;


namespace tra.osi
{

    public class WalletResult
    {
        public string Mnemonic { get; set; }       // e.g., "trend coral ...": store offline
        public string PrivateKeyHex { get; set; }  // 64-hex, no "0x": store offline
        public string Address { get; set; }        // 0x... (BSC)
    }

    public interface IBlockchainHelper
    {
        Task<decimal> GetBalance(string pWalletAddress, string pTokenAddr = "");
        WalletResult CreateNewWallet(int wordCount = 12);
        WalletResult RestoreFromMnemonic(string mnemonic, string passphrase = null, int accountIndex = 0);
        (string Address, string PrivateKeyHex) FromPrivateKey(string privateKeyHex);

        Task<decimal> GetPrice(string pTokenAddr = "");

        Task<string> SwapTokenToToken(string privateKey, string fromToken, string toToken, decimal Amount);

        Task<string> WbnbToBnb(string privateKey, decimal Amount);
        Task<string> BnbToWbnb(string privateKey, decimal Amount);

        Task<string> Transfer(string privateKey, string recipient, decimal amount, string tokenAddress = "");


    }
    public class BlockchainHelper : IBlockchainHelper
    {

        private async Task<int> _GetDecimalsAsync(string token)
        {
            var testWallet = this.CreateNewWallet();
            var account = new Account(testWallet.PrivateKeyHex);

            var web3 = new Web3(account, this._rpc);
            web3.TransactionManager.UseLegacyAsDefault = true;
            var erc20 = web3.Eth.ERC20.GetContractService(token);
            return await erc20.DecimalsQueryAsync();
        }


        private int SlippageBps = 50;
        private int DeadlineSeconds = 300;

        private readonly string _rpc;

        // Router (PancakeSwap V2)
        private readonly string _Router = "0x10ED43C718714eb63d5aA57B78B54704E256024E";
        private readonly string _USDT = "0x55d398326f99059fF775485246999027B3197955";
        private readonly string _WBNB = "0xbb4Cdb9CBd36B01bD1cBaEBF2De08d9173bc095c";

        private BigInteger MaxUint = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");
        public BlockchainHelper(string pRpc = "https://bsc-dataseed.binance.org")
        {
            _rpc = pRpc;

        }


        public async Task<string> SwapTokenToToken(string privateKey, string fromToken, string toToken, decimal Amount)
        {
            var account = new Account(privateKey);

            var web3 = new Web3(account, this._rpc);
            web3.TransactionManager.UseLegacyAsDefault = true;
            var fromTokenDec = await this._GetDecimalsAsync(fromToken);
            var toTokenDec = await this._GetDecimalsAsync(toToken);
            var amountIn = Web3.Convert.ToWei(Amount, fromTokenDec);
            await EnsureApprovalAsync(privateKey, fromToken, _Router, amountIn);
            var minAmount = await this.GetMinOutAsync(fromToken, toToken, Amount, fromTokenDec, toTokenDec, this.SlippageBps);
            string tx = await this.SwapExactTokensForTokensAsync(privateKey, fromToken, toToken, Amount, minAmount, fromTokenDec);
            return tx;

        }

        private async Task<BigInteger[]> GetAmountsOutAsync(string tokenIn, string tokenOut, BigInteger amountIn)
        {
            var testWallet = this.CreateNewWallet();
            var account = new Account(testWallet.PrivateKeyHex);

            var web3 = new Web3(account, this._rpc);
            web3.TransactionManager.UseLegacyAsDefault = true;
            var router = web3.Eth.GetContract(_RouterAbi, _Router);
            var func = router.GetFunction("getAmountsOut");
            var path = new string[] { tokenIn, tokenOut };
            return (await func.CallAsync<List<BigInteger>>(amountIn, path)).ToArray();
        }



        public async Task<decimal> GetPrice(string pTokenAddr = "")
        {

            if (string.IsNullOrWhiteSpace(pTokenAddr) || pTokenAddr == _WBNB)
            {
                pTokenAddr = _WBNB;
                decimal usdt = 1;
                var _USDTDecimals = await this._GetDecimalsAsync(_USDT);
                var usdtAmount = Web3.Convert.ToWei(usdt, _USDTDecimals);
                var tokenAmount = await this.GetAmountsOutAsync(_USDT, pTokenAddr, usdtAmount);
                var tokenDec = await this._GetDecimalsAsync(pTokenAddr);
                decimal token = Web3.Convert.FromWei(tokenAmount[1], tokenDec);
                token = (1 * usdt) / token;
                return token;
            }
            else
            {

                var bnbPrice = await GetPrice();
                decimal wbnb = 1 / bnbPrice;
                var _WBNBDecimals = await this._GetDecimalsAsync(_WBNB);
                var wbnbAmount = Web3.Convert.ToWei(wbnb, _WBNBDecimals);
                var tokenAmount = await this.GetAmountsOutAsync(_WBNB, pTokenAddr, wbnbAmount);
                var tokenDec = await this._GetDecimalsAsync(pTokenAddr);
                decimal token = Web3.Convert.FromWei(tokenAmount[1], tokenDec);
                token = 1 / token;
                return token;
            }



        }

        public async Task<decimal> GetBalance(string pWalletAddress, string pTokenAddr = "")
        {

            try
            {

                var testWallet = this.CreateNewWallet();
                var account = new Account(testWallet.PrivateKeyHex);

                var web3 = new Web3(account, this._rpc);
                web3.TransactionManager.UseLegacyAsDefault = true;

                if (string.IsNullOrWhiteSpace(pTokenAddr))
                {
                    var Balance = await web3.Eth.GetBalance.SendRequestAsync(pWalletAddress);
                    var bnbAmount = Web3.Convert.FromWei(Balance.Value);
                    return bnbAmount;
                }
                else
                {

                    var balanceOfFunctionMessage = new BalanceOfFunction()
                    {
                        Owner = pWalletAddress,
                    };

                    var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
                    var balance1 = await balanceHandler.QueryAsync<BigInteger>(pTokenAddr, balanceOfFunctionMessage);
                    var osirisBalance = Web3.Convert.FromWei(balance1);
                    return osirisBalance;
                }
            }
            catch
            {
                return 0;
            }
        }




        /// <summary>
        /// Creates a brand new wallet (BIP39/44) and returns mnemonic, private key, and BSC address.
        /// IMPORTANT: You are responsible for storing the mnemonic/private key securely.
        /// </summary>
        public WalletResult CreateNewWallet(int wordCount = 12)
        {
            // Generate a new mnemonic and derive the first account using the standard ETH/BSC path
            // BSC uses Ethereum keys/addresses: m/44'/60'/0'/0/0

            var wallet = new Wallet(Wordlist.English, WordCount.Twelve);
            var account = wallet.GetAccount(0);

            var privateKeyHex = account.PrivateKey; // 64-char hex (no 0x)
            var address = account.Address;          // 0x... (BSC compatible)

            return new WalletResult
            {
                Mnemonic = string.Join(" ", wallet.Words),            // keep offline!
                PrivateKeyHex = privateKeyHex,      // keep offline!
                Address = address
            };
        }

        /// <summary>
        /// (Optional) Re-derive the first account from an existing mnemonic.
        /// </summary>
        public WalletResult RestoreFromMnemonic(string mnemonic, string passphrase = null, int accountIndex = 0)
        {
            var wallet = new Wallet(mnemonic, passphrase);
            var account = wallet.GetAccount(accountIndex);

            return new WalletResult
            {
                Mnemonic = mnemonic,
                PrivateKeyHex = account.PrivateKey,
                Address = account.Address
            };
        }

        /// <summary>
        /// (Optional) Create from a raw 32-byte private key hex and get its address.
        /// </summary>
        public (string Address, string PrivateKeyHex) FromPrivateKey(string privateKeyHex)
        {
            var key = new EthECKey(privateKeyHex);
            var address = key.GetPublicAddress(); // 0x...
            return (address, privateKeyHex);
        }


        private async Task<BigInteger> GetMinOutAsync(
       string tokenIn, string tokenOut,
       decimal amountInDecimal,
       int tokenInDecimals, int tokenOutDecimals,
       int slippageBps)
        {
            var amountIn = Web3.Convert.ToWei(amountInDecimal, tokenInDecimals);
            var amounts = await GetAmountsOutAsync(tokenIn, tokenOut, amountIn);
            var expectedOut = amounts[^1];
            // minOut = expectedOut * (1 - slippageBps/10000)
            var slip = (decimal)slippageBps / 10000m;
            var minOutDecimal = Web3.Convert.FromWei(expectedOut, tokenOutDecimals) * (1m - slip);
            return Web3.Convert.ToWei(minOutDecimal, tokenOutDecimals);
        }


        public async Task<string> WbnbToBnb(string privateKey, decimal Amount)
        {
            //withdraw
            var account = new Account(privateKey);

            var web3 = new Web3(account, this._rpc);
            web3.TransactionManager.UseLegacyAsDefault = true;
            var wbnbDec = await this._GetDecimalsAsync(_WBNB);

            var amountIn = Web3.Convert.ToWei(Amount, wbnbDec);
            var wbnb = web3.Eth.GetContract(_WbnbAbi, _WBNB);
            var withdrawFunc = wbnb.GetFunction("withdraw");
            // Send tx
            var gasEstimate = await withdrawFunc.EstimateGasAsync(
                from: account.Address,
                null,
                value: null,
                functionInput: amountIn
            );


            var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
            var txHash = await withdrawFunc.SendTransactionAsync(account.Address, gas: gasEstimate, gasPrice: gasPrice, value: null, functionInput: amountIn);
            return txHash;
        }

        public async Task<string> BnbToWbnb(string privateKey, decimal Amount)
        {
            //deposit
            var account = new Account(privateKey);

            var web3 = new Web3(account, this._rpc);
            web3.TransactionManager.UseLegacyAsDefault = true;
            var wbnbDec = await this._GetDecimalsAsync(_WBNB);

            var amountIn = Web3.Convert.ToWei(Amount, wbnbDec);
            var wbnb = web3.Eth.GetContract(_WbnbAbi, _WBNB);
            var depositFunc = wbnb.GetFunction("deposit");
            // Send tx
            var gasEstimate = await depositFunc.EstimateGasAsync(
                from: account.Address,
                null,
                value: new HexBigInteger(amountIn)
            );


            var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
            var txHash = await depositFunc.SendTransactionAsync(account.Address, gas: gasEstimate, gasPrice: gasPrice, value: new HexBigInteger(amountIn));
            return txHash;
        }

        private async Task<string> SwapExactTokensForTokensAsync(string privateKey,
            string tokenIn, string tokenOut,
            decimal amountInDecimal,
            BigInteger amountOutMin,
            int tokenInDecimals)
        {
            var amountIn = Web3.Convert.ToWei(amountInDecimal, tokenInDecimals);
            var deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + DeadlineSeconds;
            var account = new Account(privateKey);

            var web3 = new Web3(account, this._rpc);
            web3.TransactionManager.UseLegacyAsDefault = true;
            var router = web3.Eth.GetContract(_RouterAbi, _Router);
            var swap = router.GetFunction("swapExactTokensForTokensSupportingFeeOnTransferTokens");

            var path = new string[] { tokenIn, tokenOut };

            // Send tx
            var gasEstimate = await swap.EstimateGasAsync(
                from: account.Address,
                null,
                value: null,
                functionInput: new object[] {
                amountIn,
                amountOutMin,
                path,
                account.Address,
                new BigInteger(deadline)
                }
            );


            var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
            var receipt = await swap.SendTransactionAndWaitForReceiptAsync(
                from: account.Address,
                gas: gasEstimate,
                gasPrice: gasPrice,
                value: null,
                functionInput: new object[] {
                amountIn,
                amountOutMin,
                path,
                account.Address,
                new BigInteger(deadline)
                });

            Console.WriteLine($"Swap tx mined: {receipt.TransactionHash} | Status: {receipt.Status}");
            if(receipt.Status.Value != 1)
            {
                throw new Exception($"Transaction {receipt.TransactionHash} fail status : {receipt.Status}");
            }
            return receipt.TransactionHash;
        }

        private async Task EnsureApprovalAsync(string privateKey, string token, string spender, BigInteger required)
        {
            var account = new Account(privateKey);

            var web3 = new Web3(account, this._rpc);
            web3.TransactionManager.UseLegacyAsDefault = true;
            var erc20 = web3.Eth.ERC20.GetContractService(token);
            var allowance = await erc20.AllowanceQueryAsync(account.Address, spender);
            if (allowance >= required) return;

            Console.WriteLine($"Approving {token} for {spender}...");
            var approveReceipt = await erc20.ApproveRequestAndWaitForReceiptAsync(spender, MaxUint);
            Console.WriteLine($"Approve tx: {approveReceipt.TransactionHash} | Status: {approveReceipt.Status}");
        }

        public async Task<string> Transfer(string privateKey, string recipient, decimal amount, string tokenAddress = "")
        {
            var account = new Account(privateKey);

            var web3 = new Web3(account, this._rpc);
            web3.TransactionManager.UseLegacyAsDefault = true;
            int decimalsVlaue = 18;
            if (!string.IsNullOrWhiteSpace(tokenAddress))
                decimalsVlaue = await this._GetDecimalsAsync(tokenAddress);
            else
                decimalsVlaue = await this._GetDecimalsAsync(_WBNB);

            var amountIn = Web3.Convert.ToWei(amount, decimalsVlaue);

            if (!string.IsNullOrWhiteSpace(tokenAddress))
            {
                //transfer token


                // Create a transfer function message
                var transfer = new TransferFunction()
                {
                    To = recipient,
                    Value = amountIn
                };

                // Create a transaction handler for the transfer function
                var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();


                // Execute the transaction and wait for the receipt
                var transactionReceipt = await transferHandler.SendRequestAndWaitForReceiptAsync(tokenAddress, transfer);


                if(transactionReceipt.Status.Value != 1)
                {
                    throw new Exception($"Transaction : {transactionReceipt.TransactionHash} fail , status {transactionReceipt.Status}");
                }


                return transactionReceipt.TransactionHash;
            }
            else
            {
                //transfer BNB

                // 1) Convert requested amount to Wei
                BigInteger requestedWei = amountIn;

                // Fetch gas price
                BigInteger gasPriceWei = await web3.Eth.GasPrice.SendRequestAsync();

                // Estimate gas for this transfer
                var estimatedGas = await web3.Eth.GetEtherTransferService()
                                                 .EstimateGasAsync(recipient, amount);

                BigInteger gasLimit = estimatedGas;
                BigInteger gasCostWei = gasPriceWei * gasLimit;






                // 3) Subtract gas cost from requested amount
                BigInteger finalValueWei = requestedWei - gasCostWei;

                // Send BNB directly
                var txnHash = await web3.Eth.GetEtherTransferService()
                    .TransferEtherAndWaitForReceiptAsync(recipient, Web3.Convert.FromWei(finalValueWei));


				if (txnHash.Status.Value != 1)
				{
					throw new Exception($"Transaction : {txnHash.TransactionHash} fail , status {txnHash.Status}");
				}

				return txnHash.TransactionHash;
			}
		}




		// ======= Minimal Router ABI (only what we call) =======
        private const string _RouterAbi = @"[{""inputs"":[{""internalType"":""address"",""name"":""_factory"",""type"":""address""},{""internalType"":""address"",""name"":""_WETH"",""type"":""address""}],""stateMutability"":""nonpayable"",""type"":""constructor""},{""inputs"":[],""name"":""WETH"",""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""tokenA"",""type"":""address""},{""internalType"":""address"",""name"":""tokenB"",""type"":""address""},{""internalType"":""uint256"",""name"":""amountADesired"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountBDesired"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountAMin"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountBMin"",""type"":""uint256""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""addLiquidity"",""outputs"":[{""internalType"":""uint256"",""name"":""amountA"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountB"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""liquidity"",""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""token"",""type"":""address""},{""internalType"":""uint256"",""name"":""amountTokenDesired"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountTokenMin"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountETHMin"",""type"":""uint256""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""addLiquidityETH"",""outputs"":[{""internalType"":""uint256"",""name"":""amountToken"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountETH"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""liquidity"",""type"":""uint256""}],""stateMutability"":""payable"",""type"":""function""},{""inputs"":[],""name"":""factory"",""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountOut"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""reserveIn"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""reserveOut"",""type"":""uint256""}],""name"":""getAmountIn"",""outputs"":[{""internalType"":""uint256"",""name"":""amountIn"",""type"":""uint256""}],""stateMutability"":""pure"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountIn"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""reserveIn"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""reserveOut"",""type"":""uint256""}],""name"":""getAmountOut"",""outputs"":[{""internalType"":""uint256"",""name"":""amountOut"",""type"":""uint256""}],""stateMutability"":""pure"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountOut"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""}],""name"":""getAmountsIn"",""outputs"":[{""internalType"":""uint256[]"",""name"":""amounts"",""type"":""uint256[]""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountIn"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""}],""name"":""getAmountsOut"",""outputs"":[{""internalType"":""uint256[]"",""name"":""amounts"",""type"":""uint256[]""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountA"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""reserveA"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""reserveB"",""type"":""uint256""}],""name"":""quote"",""outputs"":[{""internalType"":""uint256"",""name"":""amountB"",""type"":""uint256""}],""stateMutability"":""pure"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""tokenA"",""type"":""address""},{""internalType"":""address"",""name"":""tokenB"",""type"":""address""},{""internalType"":""uint256"",""name"":""liquidity"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountAMin"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountBMin"",""type"":""uint256""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""removeLiquidity"",""outputs"":[{""internalType"":""uint256"",""name"":""amountA"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountB"",""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""token"",""type"":""address""},{""internalType"":""uint256"",""name"":""liquidity"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountTokenMin"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountETHMin"",""type"":""uint256""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""removeLiquidityETH"",""outputs"":[{""internalType"":""uint256"",""name"":""amountToken"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountETH"",""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""token"",""type"":""address""},{""internalType"":""uint256"",""name"":""liquidity"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountTokenMin"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountETHMin"",""type"":""uint256""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""removeLiquidityETHSupportingFeeOnTransferTokens"",""outputs"":[{""internalType"":""uint256"",""name"":""amountETH"",""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""token"",""type"":""address""},{""internalType"":""uint256"",""name"":""liquidity"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountTokenMin"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountETHMin"",""type"":""uint256""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""},{""internalType"":""bool"",""name"":""approveMax"",""type"":""bool""},{""internalType"":""uint8"",""name"":""v"",""type"":""uint8""},{""internalType"":""bytes32"",""name"":""r"",""type"":""bytes32""},{""internalType"":""bytes32"",""name"":""s"",""type"":""bytes32""}],""name"":""removeLiquidityETHWithPermit"",""outputs"":[{""internalType"":""uint256"",""name"":""amountToken"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountETH"",""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""token"",""type"":""address""},{""internalType"":""uint256"",""name"":""liquidity"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountTokenMin"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountETHMin"",""type"":""uint256""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""},{""internalType"":""bool"",""name"":""approveMax"",""type"":""bool""},{""internalType"":""uint8"",""name"":""v"",""type"":""uint8""},{""internalType"":""bytes32"",""name"":""r"",""type"":""bytes32""},{""internalType"":""bytes32"",""name"":""s"",""type"":""bytes32""}],""name"":""removeLiquidityETHWithPermitSupportingFeeOnTransferTokens"",""outputs"":[{""internalType"":""uint256"",""name"":""amountETH"",""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""address"",""name"":""tokenA"",""type"":""address""},{""internalType"":""address"",""name"":""tokenB"",""type"":""address""},{""internalType"":""uint256"",""name"":""liquidity"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountAMin"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountBMin"",""type"":""uint256""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""},{""internalType"":""bool"",""name"":""approveMax"",""type"":""bool""},{""internalType"":""uint8"",""name"":""v"",""type"":""uint8""},{""internalType"":""bytes32"",""name"":""r"",""type"":""bytes32""},{""internalType"":""bytes32"",""name"":""s"",""type"":""bytes32""}],""name"":""removeLiquidityWithPermit"",""outputs"":[{""internalType"":""uint256"",""name"":""amountA"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountB"",""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountOut"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""swapETHForExactTokens"",""outputs"":[{""internalType"":""uint256[]"",""name"":""amounts"",""type"":""uint256[]""}],""stateMutability"":""payable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountOutMin"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""swapExactETHForTokens"",""outputs"":[{""internalType"":""uint256[]"",""name"":""amounts"",""type"":""uint256[]""}],""stateMutability"":""payable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountOutMin"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""swapExactETHForTokensSupportingFeeOnTransferTokens"",""outputs"":[],""stateMutability"":""payable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountIn"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountOutMin"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""swapExactTokensForETH"",""outputs"":[{""internalType"":""uint256[]"",""name"":""amounts"",""type"":""uint256[]""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountIn"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountOutMin"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""swapExactTokensForETHSupportingFeeOnTransferTokens"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountIn"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountOutMin"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""swapExactTokensForTokens"",""outputs"":[{""internalType"":""uint256[]"",""name"":""amounts"",""type"":""uint256[]""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountIn"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountOutMin"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""swapExactTokensForTokensSupportingFeeOnTransferTokens"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountOut"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountInMax"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""swapTokensForExactETH"",""outputs"":[{""internalType"":""uint256[]"",""name"":""amounts"",""type"":""uint256[]""}],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""amountOut"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""amountInMax"",""type"":""uint256""},{""internalType"":""address[]"",""name"":""path"",""type"":""address[]""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""deadline"",""type"":""uint256""}],""name"":""swapTokensForExactTokens"",""outputs"":[{""internalType"":""uint256[]"",""name"":""amounts"",""type"":""uint256[]""}],""stateMutability"":""nonpayable"",""type"":""function""},{""stateMutability"":""payable"",""type"":""receive""}]";
        private const string _WbnbAbi = @"[{""constant"":true,""inputs"":[],""name"":""name"",""outputs"":[{""name"":"""",""type"":""string""}],""payable"":false,""stateMutability"":""view"",""type"":""function""},{""constant"":false,""inputs"":[{""name"":""guy"",""type"":""address""},{""name"":""wad"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":"""",""type"":""bool""}],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""},{""constant"":true,""inputs"":[],""name"":""totalSupply"",""outputs"":[{""name"":"""",""type"":""uint256""}],""payable"":false,""stateMutability"":""view"",""type"":""function""},{""constant"":false,""inputs"":[{""name"":""src"",""type"":""address""},{""name"":""dst"",""type"":""address""},{""name"":""wad"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""name"":"""",""type"":""bool""}],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""},{""constant"":false,""inputs"":[{""name"":""wad"",""type"":""uint256""}],""name"":""withdraw"",""outputs"":[],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""},{""constant"":true,""inputs"":[],""name"":""decimals"",""outputs"":[{""name"":"""",""type"":""uint8""}],""payable"":false,""stateMutability"":""view"",""type"":""function""},{""constant"":true,""inputs"":[{""name"":"""",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":"""",""type"":""uint256""}],""payable"":false,""stateMutability"":""view"",""type"":""function""},{""constant"":true,""inputs"":[],""name"":""symbol"",""outputs"":[{""name"":"""",""type"":""string""}],""payable"":false,""stateMutability"":""view"",""type"":""function""},{""constant"":false,""inputs"":[{""name"":""dst"",""type"":""address""},{""name"":""wad"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""name"":"""",""type"":""bool""}],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""},{""constant"":false,""inputs"":[],""name"":""deposit"",""outputs"":[],""payable"":true,""stateMutability"":""payable"",""type"":""function""},{""constant"":true,""inputs"":[{""name"":"""",""type"":""address""},{""name"":"""",""type"":""address""}],""name"":""allowance"",""outputs"":[{""name"":"""",""type"":""uint256""}],""payable"":false,""stateMutability"":""view"",""type"":""function""},{""payable"":true,""stateMutability"":""payable"",""type"":""fallback""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""src"",""type"":""address""},{""indexed"":true,""name"":""guy"",""type"":""address""},{""indexed"":false,""name"":""wad"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""src"",""type"":""address""},{""indexed"":true,""name"":""dst"",""type"":""address""},{""indexed"":false,""name"":""wad"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""dst"",""type"":""address""},{""indexed"":false,""name"":""wad"",""type"":""uint256""}],""name"":""Deposit"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""src"",""type"":""address""},{""indexed"":false,""name"":""wad"",""type"":""uint256""}],""name"":""Withdrawal"",""type"":""event""}]";
    }
}