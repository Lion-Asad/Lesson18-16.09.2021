using System;

namespace Lesson18_16._09._2021
{
    static class IO
    {
        static private bool DEBUG = false;
        static private bool COLORFUL = true;

        static public void Print<T>(T what, ConsoleColor color = ConsoleColor.White, bool newLine = true)
        {
            Console.ForegroundColor = COLORFUL ? color : ConsoleColor.White;
            Console.Write(what);

            if (newLine) Console.Write("\n");

            Console.ResetColor();
        }


        static public void Debug(string what, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true)
        {
            if (!DEBUG) return;

            Print(what, color, newLine);
        }


        static public T Get<T>(string what = "")
        {
            IO.Print(what, ConsoleColor.Yellow, false);
            var input = (Console.ReadLine());

            return (T)Convert.ChangeType(input, typeof(T));
        }
    }

    class Account
    {
        public int Id { get; set; }
        public string Acc { get; set; }
        public int Is_Active { get; set; }
        public DateTime Created_At { get; set; }
        public DateTime? Updated_At { get; set; } // This can be null

        public void GetInfo() { IO.Print($"{Id}\t{Acc}\t{Is_Active}\t{Created_At}\t{Updated_At}\tBalance: ", ConsoleColor.Blue, false); }


        public Account() { }

        public Account(string acc, int is_active, DateTime created_at)
        {
            Acc = acc;
            Is_Active = is_active;
            Created_At = created_at;
            IO.Debug("Created new account instance.");
        }
    }

    public class Transaction
    {
        public int Id { get; set; }
        public int Account_Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime Created_At { get; set; }
    }

    public class SQL
    {
        private SqlConnection cnn;

        public bool AlreadyExist(string acc)
        {
            var cmd = cnn.CreateCommand();
            cmd.CommandText = $"SELECT Account FROM Account WHERE Account = '{acc}'";

            var data = cmd.ExecuteReader(); IO.Debug("reader OPENED !");
            bool exist = !!data.Read();
            data.Close(); IO.Debug("reader CLOSED !");
            return exist;
        }
        public void CreateAccount()
        {

            var newAccount = new Account(IO.Get<string>("Input Account number in xxxxx format: "), 0, DateTime.Now);

            try
            {
                if (String.IsNullOrEmpty(newAccount.Acc)) throw new Exception("You must specify account number !");
                if (AlreadyExist(newAccount.Acc)) throw new Exception("This account already exists !");
                var cmd = cnn.CreateCommand();

                cmd.CommandText = "INSERT INTO [dbo].[Account]([Account], [Is_Active], [Created_At]) VALUES (@Acc, @IsActive, @Created_At)";
                cmd.Parameters.AddWithValue("@Acc", newAccount.Acc);
                cmd.Parameters.AddWithValue("@IsActive", newAccount.Is_Active);
                cmd.Parameters.AddWithValue("@Created_At", newAccount.Created_At);

                var res = cmd.ExecuteNonQuery();
                if (res <= 0) throw new Exception("The account was not created !");


                IO.Print($"Account {newAccount.Acc} created successfully !", ConsoleColor.Green);

                cmd.Parameters.Clear(); IO.Debug("Parameters cleared !");
            }
            catch (Exception ex)
            {
                IO.Print(ex.Message, ConsoleColor.Red);
            }
        }
        public void ShowAccounts(string cnnString)
        {
            var cmd = cnn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Account";
            var data = cmd.ExecuteReader(); IO.Debug("Reader OPENED !");


            IO.Print("Id\tAccount\tActive\tCreation Date\t\tUpdate Date");
            while (data.Read())
            {
                var tempAcc = new Account();
                tempAcc.Id = int.Parse(data["Id"].ToString());
                tempAcc.Acc = data["Account"].ToString();
                tempAcc.Is_Active = int.Parse(data["Is_Active"].ToString());
                tempAcc.Created_At = DateTime.Parse(data["Created_At"].ToString());


                var tUpdatedAt = data["Updated_At"].ToString();


                tempAcc.Updated_At = !String.IsNullOrEmpty(tUpdatedAt) ? DateTime.Parse(tUpdatedAt) : null;

                tempAcc.GetInfo();
                IO.Print($"{GetAccountBalance(tempAcc.Acc, cnnString)}");
            }
            data.Close(); IO.Debug("Reader CLOSED !");
        }
        public decimal GetAccountBalance(string acc, string cnnString)
        {
            decimal balance = 0;

            var conn = new SqlConnection(cnnString);
            conn.Open();

            var command = conn.CreateCommand();

            command.CommandText = $"select sum([Amount]) from [dbo].[Transactions] LEFT JOIN [dbo].[Account] ON [Account].[Id] = [Transactions].[Account_Id] WHERE Account = '{acc}'";

            var data = command.ExecuteReader(); IO.Debug("reader OPENED !");
            while (data.Read()) balance = !string.IsNullOrEmpty(data.GetValue(0)?.ToString()) ? data.GetDecimal(0) : 0;

            data.Close(); IO.Debug("reader CLOSED !");
            conn.Close();
            return balance;
        }
        public void Transfer(string donor, string reciever, decimal amount, string cnnString)
        {
            IO.Debug("Begginning transfer ...");
            var tarnsCnn = new SqlConnection(cnnString);
            tarnsCnn.Open();

            var sqlTrans = tarnsCnn.BeginTransaction();

            var donorBalance = GetAccountBalance(donor, cnnString);
            var recieverBalance = GetAccountBalance(donor, cnnString);
            try
            {
                if (String.IsNullOrEmpty(donor) || String.IsNullOrEmpty(reciever)) throw new Exception("Account can not be empty !");
                if (amount <= 0) throw new Exception("Amount must be positive !");
                var donorID = GetIdByAccount(donor);
                var recieverID = GetIdByAccount(reciever);
                if (donorID == -1 || recieverID == -1) throw new Exception("One of the accounts was not found !");
                if (donorBalance - amount < 0) throw new Exception("Not enought balance in donor's account !");




                {
                    var transact1 = new Transaction()
                    {
                        Account_Id = donorID,

                        Amount = -1 * amount,
                        Created_At = DateTime.Now
                    };
                    var query1 = "insert into [dbo].[Transactions]([Account_Id], [Amount], [Created_At]) VALUES (@Id, @amount, GETDATE())";
                    var cmd = tarnsCnn.CreateCommand();
                    cmd.CommandText = query1;
                    cmd.Transaction = sqlTrans;
                    cmd.Parameters.AddWithValue("@Id", transact1.Account_Id);
                    cmd.Parameters.AddWithValue("@amount", transact1.Amount);

                    var res = cmd.ExecuteNonQuery();
                    if (res > 0) IO.Print($"Transfered {amount} from {donor}");
                    else throw new Exception("An error occured !");
                    cmd.Parameters.Clear();
                }

                {
                    var transact2 = new Transaction()
                    {
                        Account_Id = recieverID,

                        Amount = 1 * amount,
                        Created_At = DateTime.Now
                    };
                    var query2 = "insert into [dbo].[Transactions]([Account_Id], [Amount], [Created_At]) VALUES (@Id, @amount, GETDATE())";
                    var cmd = tarnsCnn.CreateCommand();
                    cmd.CommandText = query2;
                    cmd.Transaction = sqlTrans;
                    cmd.Parameters.AddWithValue("@Id", transact2.Account_Id);
                    cmd.Parameters.AddWithValue("@amount", transact2.Amount);

                    var res = cmd.ExecuteNonQuery();
                    if (res > 0) IO.Print($"Transfered {amount} to {reciever}");
                    else throw new Exception("An error occured !");
                    cmd.Parameters.Clear();
                }



                sqlTrans.Commit(); IO.Debug("Commiting !");
            }
            catch (Exception ex)
            {
                IO.Print(ex.Message, ConsoleColor.Red);
                IO.Print("ROLLING BACK !", ConsoleColor.Red);
                sqlTrans.Rollback();
            }
            finally
            {
                tarnsCnn.Close();
                donorBalance = GetAccountBalance(donor, cnnString);
                recieverBalance = GetAccountBalance(reciever, cnnString);
                if (donorBalance == 0)
                {
                    var updateCmd = cnn.CreateCommand();

                    updateCmd.CommandText = $"UPDATE [dbo].[Account] SET [Is_Active] = 0 WHERE [Account] = '{donor}'";
                    var res = updateCmd.ExecuteNonQuery();
                    if (res > 0) IO.Debug("Toggled is active for donor !");
                }


                if (recieverBalance > 0)
                {
                    var updateCmd = cnn.CreateCommand();
                    updateCmd.CommandText = $"UPDATE [dbo].[Account] SET [Is_Active] = 1 WHERE [Account] = '{reciever}'";
                    var res = updateCmd.ExecuteNonQuery();
                    if (res > 0) IO.Debug("Toggled is active !");
                }
            }
        }
        private int GetIdByAccount(string acc)
        {
            IO.Debug($"Getting id of {acc}");
            int ID = -1;

            var cmd = cnn.CreateCommand();
            cmd.CommandText = $"select Id from [dbo].[Account] where [Account] = '{acc}'";

            var data = cmd.ExecuteReader(); IO.Debug("reader OPENED !");

            while (data.Read()) ID = data.GetInt32(0);

            data.Close(); IO.Debug("reader CLOSED !");

            return ID;
        }

        public SQL() { }
        public SQL(string cnnString)
        {
            cnn = new SqlConnection(cnnString); IO.Debug($"Sql connection initialized with {cnnString}");
            cnn.Open(); IO.Debug("Sql connection OPENED!");
        }
    }

    class Program
    {
        public static void Main()
        {
            var cnnString = "Data Source=localhost;Initial Catalog=AlifAcademy;User ID=sa;Password=qwerty111!";
            var SQL = new SQL(cnnString);

            bool running = true;
            int i = 0;
            while (running)
            {
                IO.Get<string>("Pess any key ...");

                if (i % 2 == 0) Console.Clear(); ++i;


                IO.Print("1. Create Account\t 2. Show Accounts\t3. Transfer\t4. Get account balance\t5. Get ID by account");
                var cmd = IO.Get<string>("->: ");

                switch (cmd)
                {
                    case "1":
                        SQL.CreateAccount();
                        break;
                    case "2":
                        SQL.ShowAccounts(cnnString);
                        break;
                    case "3":
                        SQL.Transfer(IO.Get<string>("Input donor account: "), IO.Get<string>("Input reciever account: "), IO.Get<decimal>("Input the amount: "), cnnString);
                        break;
                    case "4":
                        IO.Print(SQL.GetAccountBalance(IO.Get<string>("Input account number: "), cnnString), ConsoleColor.Green);
                        break;

                    default:
                        IO.Print("Invalid command !", ConsoleColor.Red);
                        running = false;
                        break;
                }
            }
        }
    }
}
