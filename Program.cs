using System.Text.Json;
using MySql.Data.MySqlClient;
using System;
using System.Data;

public class IwaiServer{

    static MySqlConnection ConnectSQL(){
        var conn = new MySqlConnection("server=localhost;user id=root;password=root;database=IwaiDB");
        conn.Open();
        return conn;
    }
    static void Main(string[] args){
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment()){
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.MapGet("/", () => "Hello World!");

        app.MapGet("/GetAllList",()=>{
            var conn = ConnectSQL();

            // データを取得するテーブル
            DataTable tbl = new DataTable();

            // SQLを実行する
            MySqlDataAdapter dataAdp = new MySqlDataAdapter("SELECT * FROM Items;", conn);
            dataAdp.Fill(tbl);

            // DataTable を List<Item> に変換
            Item[] items = new Item[tbl.Rows.Count];
            for(int i = 0;i<tbl.Rows.Count;i++){
                items[i] = new Item{
                    Id = int.Parse(tbl.Rows[i]["id"].ToString()),
                    Name = tbl.Rows[i]["name"].ToString(),
                    Tag = tbl.Rows[i]["tag"].ToString(),
                    Quantity = int.Parse(tbl.Rows[i]["quantity"].ToString()),
                    CreatedAt = DateTime.Parse(tbl.Rows[i]["created_at"].ToString()),
                    UpdatedAt = DateTime.Parse(tbl.Rows[i]["updated_at"].ToString())
                };
            }
            return JsonSerializer.Serialize(new Result{Code = 200,Message = JsonSerializer.Serialize(items)});
        }).WithName("GetList").WithOpenApi();

        app.MapGet("/add_data", (IQueryCollection query) =>{
            // クエリパラメータの存在を確認し、存在しない場合はエラーメッセージを表示
            if (!query.ContainsKey("name") ||!query.ContainsKey("tag") ||!query.ContainsKey("count")){
                return JsonSerializer.Serialize(new Result{Code = 400, Message = "Query Parameter is not found.\nNeed name,tag,count Query Parameter."});
            }
            MySqlTransaction trans = null;
            string name = query["name"].ToString();
            string tags = query["tag"].ToString();
            DateTime localDate = DateTime.Now;
            int count = int.Parse(query["count"].ToString());
            var conn = ConnectSQL();
            string sqlCmd = $"INSERT INTO Items (name, tag, quantity, created_at, updated_at)VALUES ('{name}', '{tags}', {count}, {localDate.ToString("yyyy-mm-dd hh:mm:ss")}, {localDate.ToString("yyyy-mm-dd hh:mm:ss")});";
            MySqlCommand cmd = new MySqlCommand(sqlCmd, conn);
            try{
                trans = cmd.Connection.BeginTransaction(IsolationLevel.ReadCommitted);
                cmd.ExecuteNonQuery();
                trans.Commit();
            }catch (MySqlException mse){
                trans.Rollback();
                conn.Close();
                return JsonSerializer.Serialize(new Result{Code = 500, Message = "Error"});
                // 例外発生時はロールバック
            }
            conn.Close();
            return JsonSerializer.Serialize(new Result{Code = 200, Message = "Success.\nData is added."});
        });
        app.Run();
    }
}

//メモ
//名前(VARCHAR(40)),タグ(TEXT),数量(smallINT),登録日時(DATETIME),更新日時(DATETIME)

class Result{
    public int Code {get;set;}
    public string Message {get;set;}
}

class Item{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Tag { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}