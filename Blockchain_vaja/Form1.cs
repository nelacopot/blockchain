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
            NetworkStream netstr = client.GetStream(); // RECEIVE je tu
            byte[] buffer = new byte[102400];

            while (!token.IsCancellationRequested)
            {
                try
                {
                    int bytesRead = await netstr.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead > 0)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        if (msg.StartsWith("[") && msg.EndsWith("]")) //prepoznava JSON
                        {

                            await Task.Run(async () =>
                            {
                                try
                                {
                                    List<Block> receivedChain = JsonSerializer.Deserialize<List<Block>>(msg);

                                    if (blockchain.ReplaceChain(receivedChain)) //če veriga boljša po pravilih konsenza...
                                    {
                                        UpdateMessages("Replaced chain with longer received chain.");
                                    }

                                    var lastBlock = receivedChain.Last();
                                    UpdateMessages($"index: {lastBlock.Index}");
                                    UpdateMessages($"diff: {lastBlock.Difficulty}");
                                    UpdateMessages($"hash: {lastBlock.Hash}");
                                    UpdateMessages($"previous hash: {lastBlock.PreviousHash}");
                                    UpdateHashes(lastBlock.Hash);
                                }
                                catch (Exception ex)
                                {
                                    UpdateMessages($"Error deserializing chain: {ex.Message}");
                                }
                            });
                        }
                        else
                        {
                            UpdateMessages($"Received: {msg}");
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    UpdateMessages("Odjemalec se je odklopil");
                }
                catch (Exception e)
                {
                    UpdateMessages($"NAPAKA na odjemalcu! ...{e.Message}");
                    break;
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
            threadClient();
        }

        private async void mineButton_Click(object sender, EventArgs e)
        {
            miningCancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => Mining(miningCancellationTokenSource.Token)); //asinhrono zažene mining (node začne generirat nove bloke)

        }

        private async Task Mining(CancellationToken token)
        {
            while (is_mining)
            {
                Block newBlock;
                int diff = blockchain.GetLatestBlock().Difficulty; //trenutna težavnost zadnjega bloka
                blockchain.AdjustDifficulty(ref diff); //prilagoditev težavnosti
                if (diff == blockchain.GetLatestBlock().Difficulty)
                {
                    //ustvari nov blok
                    newBlock = new Block(blockchain.GetLatestBlock().Index + 1, "Block data", DateTime.Now, blockchain.GetLatestBlock().Hash, blockchain.GetLatestBlock().Difficulty);
                    //previousHash poveže blok nazaj na prejšnjega
                    //timeStamp za pravila časa, prilagajanje težavnosti
                    //Difficulty... koliko dela mora rudar opraviti
                }
                else
                {
                    newBlock = new Block(blockchain.GetLatestBlock().Index + 1, "Block data", DateTime.Now, blockchain.GetLatestBlock().Hash, diff);
                   
                }

                //dodaj blok v verigo
                blockchain.AddBlock(newBlock);


                //pošlje prvo celo verigo za prevzem
                await SendChainToPeers();

                //pošlji blok vsem peerjem
                //await SendBlockToPeers(newBlock);

                UpdateMessages($"Block mined and sent: {newBlock.Index}");

                await Task.Delay(4000);
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

        /*private async Task SendBlockToPeers(Block block)//SEND funkcija
        {
            string json = block.SerializeBlock();
            foreach (var peer in peers)//posljem vsem peerom
            {
                NetworkStream stream = peer.GetStream();
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(buffer, 0, buffer.Length);
            }
        }*/

        private async Task SendChainToPeers()//SEND funkcija
        {
            string json_chain = JsonSerializer.Serialize(blockchain.GetChain());
            foreach (var peer in peers)//posljem vsem peerom
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

        private void UpdateHashes(string msg)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateMessages), msg);
            }
            else
            {
                miningBox.AppendText(msg + Environment.NewLine);
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

        public Block(int index, string data, DateTime timestamp, string previousHash, int difficulty)
        {
            Index = index;
            Data = data;
            Timestamp = timestamp;
            PreviousHash = previousHash;
            Difficulty = difficulty;
            Nonce = 0;
            Hash = CalculateHash();
        }

        public string CalculateHash() // to je dejansko MINING
        {
            using (var sha256 = SHA256.Create())
            {
                var target = new string('0', Difficulty); //hash mora imeti toliko nul pred njim kot je tezavnost
                //težavnost določa kako težko je najti hash z dovolj ničlami
                string hash;
                Nonce = 0;

                do
                {
                    var input = $"{Index}{Timestamp}{Data}{PreviousHash}{Nonce}";
                    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                    hash = BitConverter.ToString(bytes).Replace("-", string.Empty);

                    Nonce++;
                } while (!hash.StartsWith(target));

                return hash;
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

        public Blockchain(int difficulty = 3, int blockGenerationInterval = 10, int difficultyAdjustmentInterval = 10)
        {
            this.difficulty = difficulty;
            this.blockGenerationInterval = blockGenerationInterval;
            this.difficultyAdjustmentInterval = difficultyAdjustmentInterval;

            chain = new List<Block>
            {
                CreateGenesisBlock()
            };
            lastDifficultyAdjustment = DateTime.Now;
        }

        private Block CreateGenesisBlock()
        {
            return new Block(0, "Genesis Block", DateTime.Now, "0", difficulty);
            //genesis blok je prvi blok, začetek blockchain-a
            //v realnih sistemih je genesis isti za vse, pri meni ima DateTime.Now, zato bo različen... verige se lahko med sabo ne bodo ujemale!!
            // ^^ta razlika je velika konceptualna razlika od pravega blockchain
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
            for (int i = 1; i < chainToValidate.Count; i++)
            {
                Block currentBlock = chainToValidate[i];
                Block previousBlock = chainToValidate[i - 1];

                // Preverimo osnovne pogoje
                if (currentBlock.Index != previousBlock.Index + 1) //prepreči preskoke/duplikate
                {
                    return false;
                }
                if (currentBlock.PreviousHash != previousBlock.Hash) //zagotovi povezavo med bloki
                {
                    return false;
                }
                if (currentBlock.Hash != currentBlock.CalculateHash()) //hash mora ustrezat podatkom
                {
                    Debug.WriteLine("hash");
                    return false;
                }
                if (currentBlock.Timestamp > DateTime.Now.AddMinutes(1)) //blok ne sme biti iz prihodnosti
                {
                    return false;
                }

                if (currentBlock.Timestamp<previousBlock.Timestamp||
                    currentBlock.Timestamp> previousBlock.Timestamp.AddMinutes(5))
                {
                return false;
                }

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
            if (chain.Count % 10 == 0 && chain.Count > 0) //vsakih 10 blokov ocenjujem hitrost rudarjenja
            {
                int latestBlockIndex = chain.Last().Index;

                // Pridobi blok, ki je bil uporabljen za zadnjo prilagoditev težavnosti
                Block previousAdjustmentBlock = chain[chain.Count - difficultyAdjustmentInterval];

                // Izračunajte pričakovani čas generiranja blokov
                TimeSpan expectedTime = TimeSpan.FromSeconds(blockGenerationInterval * difficultyAdjustmentInterval);

                // Izračunajte dejanski čas, ki je minil od zadnje prilagoditve težavnosti
                TimeSpan timeTaken = chain.Last().Timestamp - previousAdjustmentBlock.Timestamp;

                //prilagoditev tezavnosti
                if (timeTaken < expectedTime / 2) //prehitro generiranje
                {
                    diff=chain.Last().Difficulty + 1;
                }
                else if (timeTaken > expectedTime * 2) //prepočasno generiranje
                {
                    diff=chain.Last().Difficulty - 1;
                }
            }

        }
    }
}