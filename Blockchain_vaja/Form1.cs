using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Security.Policy;



namespace Blockchain_vaja
{
    public partial class Form1 : Form
    {
        private TcpListener server; //ker imam P2P omrežje
        private List<TcpClient> peers = new List<TcpClient>(); //seznam vseh peer povezav
        private readonly object chainLock = new object();
        private string username; //ne uporablja se za transakcije, samo za UI zapis
        private int serverPort; //port na katerem server posluša
        private CancellationTokenSource serverCancellationTokenSource; //mehanizmi za zaustavitev asinhronih zank
        private CancellationTokenSource clientCancellationTokenSource;
        private CancellationTokenSource miningCancellationTokenSource;
        private Blockchain blockchain; //lokalna veriga blokov (node pa vedno hrani še svojo kopijo verige)
        private bool is_mining = true;

        public Form1()
        {
            InitializeComponent();

            mineButton.Enabled = false;
            portButton.Enabled = false;
            blockchain = new Blockchain();

        }

        private void threadServer() //server je v ločeni niti
        {
            serverCancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = serverCancellationTokenSource.Token;

            Task.Run(() => ServerStart(token));
        }

        private async void ServerStart(CancellationToken token)//asinhrona naloga v ločeni niti
        {
            serverPort = new Random().Next(2000, 8000); //random port, da lahko več instant teče brez konflikta
            server = new TcpListener(IPAddress.Parse("127.0.0.1"), serverPort);
            server.Start();//node postane dosegljiv drugim

            UpdateMessages($"Strežnik teče na portu {serverPort}");

            while (!token.IsCancellationRequested) 
            {
                try
                {
                    TcpClient client = await server.AcceptTcpClientAsync(); //blokira (asinhrono) do nove povezave
                    peers.Add(client); //shrani povezavo
                    UpdateMessages("Nova instanca povezana!");
                    Task.Run(() => HandleClient(client, token)); //locena nit za vsakega odjemalca (da lahko hkrati več peer-jev)
                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested)
                    {
                        UpdateMessages($"Server error: {e.Message}");
                        break;
                    }
                }
            }

        }

        private void threadClient()
        {
            clientCancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => ClientStart(clientCancellationTokenSource.Token));
        }

        private async void ClientStart(CancellationToken token)
        {
            try
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", serverPort); //povezava s strežnikom
                //client se poveže na isti serverPort kot ga je izbral node. Instanca se poveže sama nase (tu bi v praksi bil port druge instance?)
      
                peers.Add(client);//dodam na seznam peers
                UpdateMessages($"Connected to peer at port {serverPort}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ERROR connecting to peer: {ex.Message}");
            }
        }

        private async Task HandleClient(TcpClient client, CancellationToken token) //funkcija, ki prejema podatke
        {
            using var stream = client.GetStream(); // RECEIVE je tu
            using var reader = new StreamReader(stream, Encoding.UTF8);


            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                    break;

                string msg = line.Trim();
                if (msg.StartsWith("[") && msg.EndsWith("]"))
                {
                    try
                    {
                        var receivedChain = JsonSerializer.Deserialize<List<Block>>(msg);

                        bool replaced = false;
                        if (receivedChain != null)
                        {
                            lock (chainLock)
                            {
                                replaced = blockchain.ReplaceChain(receivedChain);
                            }

                            if (replaced)
                            {
                                UpdateMessages("Replaced chain with better received chain.");

                                Block lastBlock;
                                lock (chainLock)
                                {
                                    lastBlock = blockchain.GetLatestBlock();
                                }

                                DisplayValidBlock(lastBlock);

                                //ustavi trenutni mining loop in ga ponovno zaženi
                                miningCancellationTokenSource?.Cancel();
                                miningCancellationTokenSource = new CancellationTokenSource();
                                Task.Run(() => Mining(miningCancellationTokenSource.Token));
                            }
                        }

                        var last = receivedChain?.LastOrDefault();
                        if (last != null)
                        {
                            //UpdateMessages($"index: {last.Index}");
                            //UpdateMessages($"diff: {last.Difficulty}");
                            //UpdateMessages($"hash: {last.Hash}");
                            //UpdateMessages($"previous hash: {last.PreviousHash}");
                          
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateMessages($"Error deserializing chain: {ex.Message}");
                    }
                }
                else
                {
                    UpdateMessages($"Received: {msg}");
                }
            }

            peers.Remove(client);
            client.Close();
            UpdateMessages("Povezava z odjemalcem zaprta.");
        }



        private void nodeName_TextChanged(object sender, EventArgs e)
        {
            mineButton.Enabled = true;
        }

        private void ConnectButton_Click(object sender, EventArgs e) //prebere username, zažene server in client ... node se postavi v omrežje
        {
            username = nodeName.Text;
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Vnesite ime");
                return;
            }


            threadServer(); 
            //threadClient();
        }

        private async void mineButton_Click(object sender, EventArgs e)
        {
            miningCancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => Mining(miningCancellationTokenSource.Token)); //asinhrono zažene mining (node začne generirat nove bloke)

        }

        private void DisplayValidBlock(Block block)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Block>(DisplayValidBlock), block);
                return;
            }

            miningBox.AppendText(
                $"Index: {block.Index}\r\n" +
                $"Difficulty: {block.Difficulty}\r\n" +
                $"Nonce: {block.Nonce}\r\n" +
                $"Hash: {block.Hash}\r\n" +
                $"PrevHash: {block.PreviousHash}\r\n" +
                $"Timestamp: {block.Timestamp:O}\r\n" +
                "-----------------------------\r\n"
            );
        }

        private async Task Mining(CancellationToken token)
        {
            while (is_mining && !token.IsCancellationRequested)
            {
                // 1) pod lock preberi stabilen "latest" + izračunaj difficulty
                Block latest;
                int diff;

                lock (chainLock)
                {
                    latest = blockchain.GetLatestBlock();
                    diff = latest.Difficulty;
                    blockchain.AdjustDifficulty(ref diff);
                }

                // 2) rudarjenje (brez locka) – to je najdražje in bi blokiralo druge niti
                var newBlock = new Block(
                    latest.Index + 1,
                    "Block data",
                    DateTime.UtcNow,
                    latest.Hash,
                    diff
                );

                // 3) poskusi dodati blok pod lock (da se veriga vmes ni zamenjala)
                bool added = false;
                lock (chainLock)
                {
                    // če se je vmes veriga zamenjala, latest ni več aktualen → ne dodajaj
                    var currentLatest = blockchain.GetLatestBlock();
                    if (currentLatest.Hash == newBlock.PreviousHash &&
                        currentLatest.Index + 1 == newBlock.Index)
                    {
                        blockchain.AddBlock(newBlock);
                        added = true;
                    }
                }

                if (added)
                {
                    DisplayValidBlock(newBlock);
                    await SendChainToPeers();
                    UpdateMessages($"Block mined and sent: {newBlock.Index}");
                }
                else
                {
                    // veriga se je vmes spremenila (npr. ReplaceChain), zato ta blok zavržemo
                    UpdateMessages("Mined block discarded (chain updated during mining).");
                }

                await Task.Delay(4000, token);
            }
        }

        private void Port_TextChanged(object sender, EventArgs e)
        {
            portButton.Enabled = true;
        }

        private async void portButton_Click(object sender, EventArgs e)
        {
            string peerPort = Port.Text; //rocni vpis port-a druge instance
            if (string.IsNullOrWhiteSpace(peerPort))
            {
                UpdateMessages("Please enter a valid port number.");
                return;
            }

            if (!int.TryParse(peerPort, out int portNumber) || portNumber < 1 || portNumber > 65535) //iz stringa v int portNumber
            {
                UpdateMessages("Please enter a valid port number between 1 and 65535.");
                return;
            }

            try
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", portNumber);
                peers.Add(client);//dodam na seznam peers
                UpdateMessages($"Connected to peer at port {portNumber}");
                await SendChainToPeers(); //po povezavi takoj pošljem svojo verigo... drugi jo sprejme, če je boljša

            }
            catch (Exception ex)
            {
                MessageBox.Show($"ERROR connecting to peer: {ex.Message}");
            }
        }

        private async Task SendToPeers(string msg)//SEND funkcija
        {
            foreach (var peer in peers)//posljem vsem peerom
            {
                NetworkStream stream = peer.GetStream();
                byte[] buffer = Encoding.UTF8.GetBytes(msg);
                await stream.WriteAsync(buffer, 0, buffer.Length);
            }
        }

        private async Task SendChainToPeers()
        {
            string json_chain;
            lock (chainLock)
            {
                json_chain = JsonSerializer.Serialize(blockchain.GetChain()) + "\n";
            }

            foreach (var peer in peers)
            {
                NetworkStream stream = peer.GetStream();
                byte[] buffer = Encoding.UTF8.GetBytes(json_chain);
                await stream.WriteAsync(buffer, 0, buffer.Length);
            }
        }

        private void UpdateMessages(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateMessages), message);
            }
            else
            {
                blockchainLedger.AppendText(message + Environment.NewLine);
            }
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            serverCancellationTokenSource?.Cancel();
            clientCancellationTokenSource?.Cancel();
            is_mining = false;

            try
            {
                server?.Stop();
            }
            catch (Exception ex)
            {
                UpdateMessages($"Error stopping server: {ex.Message}");
            }

            foreach (var peer in peers)
            {
                try
                {
                    peer.Close();
                }
                catch
                {
                    // Ignoriram napake ko se zapirajo
                }
            }

        }


    }

    public class Block
    {
        public int Index { get; set; }  //pozicija v verigi (narašča za 1)
        public string Data { get; set; } //vsebina bloka (transakcije... pri meni samo placeholder)
        public DateTime Timestamp { get; set; } //čas nastanka
        public string Hash { get; set; } //ID bloka (vsak bit spremembe spremeni hash)
        public string PreviousHash { get; set; } //bistvo verige... POVEZAVA NA PREJŠNJI BLOK (če spremeniš prejšnji blok se spremeni njegov hash... novi blok bo napačen)
        public int Difficulty { get; set; } //koliko ničel mora imeti hash na začetku (PoW - dokaz dela)
        public int Nonce { get; set; } //število, ki ga spreminjam pri iskanju hash-a 

        public Block() { }

        public Block(int index, string data, DateTime timestampUtc, string previousHash, int difficulty)
        {
            Index = index;
            Data = data;
            Timestamp = timestampUtc;
            PreviousHash = previousHash;
            Difficulty = difficulty;
            Nonce = 0;
            Hash = Mine();
        }

        public string ComputeHash(int nonce) // to je dejansko MINING
        {
            using var sha256 = SHA256.Create();

            string input = $"{Index}{Data}{Timestamp:O}{PreviousHash}{Difficulty}{nonce}";
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }

        public string Mine()
        {
            string target = new string('0', Difficulty);
            int nonce = 0;

            while (true)
            {
                string hash = ComputeHash(nonce);
                if (hash.StartsWith(target))
                {
                    Nonce = nonce;
                    return hash;
                }
                nonce++;
            }
        }
    }

    public class Blockchain
    {
        private List<Block> chain;
        private int difficulty;
        private int blockGenerationInterval;
        private int difficultyAdjustmentInterval;
        private DateTime lastDifficultyAdjustment;

        public Blockchain(int difficulty = 5, int blockGenerationInterval = 10, int difficultyAdjustmentInterval = 10)
        {
            this.difficulty = difficulty;
            this.blockGenerationInterval = blockGenerationInterval;
            this.difficultyAdjustmentInterval = difficultyAdjustmentInterval;

            chain = new List<Block>
            {
                CreateGenesisBlock()
            };
            lastDifficultyAdjustment = DateTime.UtcNow;
        }

        private Block CreateGenesisBlock()
        {
            return new Block(0, "Genesis Block", DateTime.UnixEpoch, "0", difficulty);
            //genesis blok je prvi blok, začetek blockchain-a
        }

        public Block GetLatestBlock()
        {
            return chain.Last();
        }

        public List<Block> GetChain()
        {
            return chain;
        }

        public void AddBlock(Block newBlock) //dodajanje po validaciji
        {
            List<Block> tempChain = new List<Block>(chain);
            tempChain.Add(newBlock);
            if (ValidateChain(tempChain))
            {
                chain.Add(newBlock);
                

                Debug.WriteLine("New block valid.");
            }
            else
            {
                Debug.WriteLine("New block is not valid.");
            }
        }

        public bool ValidateChain(List<Block> chainToValidate) //VARNOSTNA PRAVILA
        {
            for(int i = 1; i < chainToValidate.Count; i++)
            {
                Block current = chainToValidate[i];
                Block previous = chainToValidate[i - 1];

                if (current.Index != previous.Index + 1) return false;
                if (current.PreviousHash != previous.Hash) return false;

                if (current.Timestamp > DateTime.UtcNow.AddMinutes(1)) return false;
                if (current.Timestamp < previous.Timestamp.AddMinutes(-1)) return false;

                //Proof of work
                if (!current.Hash.StartsWith(new string('0', current.Difficulty))) return false;

                string recomputed = current.ComputeHash(current.Nonce);
                if(current.Hash !=  recomputed) return false;
            }
            return true;
        }

        public bool ReplaceChain(List<Block> newChain) //KONSENZ ...najboljša veriga
        {
            //novo verigo prejmem, če
            //--je validna
            //--predstavlja več dela
            if (CalculateCumulativeDiff(newChain) > CalculateCumulativeDiff(chain) && ValidateChain(newChain))
            {
                chain = newChain;
                return true;
            }
            else
            {
                return false;
            }
        }

        private double CalculateCumulativeDiff(List<Block> chain) //višnja težavnost ... več dela.. .zmaguje veriga z večjih proof of work
        {
            double cumulativeDiff = 0;

            foreach(Block block in chain)
            {
                cumulativeDiff += Math.Pow(2, block.Difficulty);
            }

            return cumulativeDiff;
        }

        public void AdjustDifficulty(ref int diff)
        {
            if (chain.Count <= difficultyAdjustmentInterval) return;

            int minedBlocksNum = chain.Count - 1;

            if (minedBlocksNum % difficultyAdjustmentInterval != 0) return;

            //prilagoditveni blok = veriga[dolžina - interval - 1] ...blok uporabljen za prilagoditev tezavnosti
            //(blok tik pred zadnjimi N bloki)
            Block adjustmentBlock = chain[chain.Count - difficultyAdjustmentInterval - 1];
            Block latestBlock = chain.Last();
            TimeSpan expectedTime = TimeSpan.FromSeconds(blockGenerationInterval * difficultyAdjustmentInterval);
            TimeSpan timeTaken = latestBlock.Timestamp - adjustmentBlock.Timestamp;
            int baseDifficulty = adjustmentBlock.Difficulty;

            if (timeTaken < expectedTime / 2)          // prehitro
                diff = baseDifficulty + 1;
            else if (timeTaken > expectedTime * 2)     // prepočasi
                diff = Math.Max(1, baseDifficulty - 1);
            else
                diff = baseDifficulty;

        }

    }
}