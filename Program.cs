using System.Text.Json;
using MySql.Data.MySqlClient;
using System.Data;
using System.Text.Encodings.Web;

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

var jsonSerializerOptions = new JsonSerializerOptions
{
    WriteIndented = true, // not necessary, but more readable
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

static MySqlConnection ConnectDB(){
    var con = new MySqlConnection("server=localhost;user id=root;password=root;database=IwaiDB");
    con.Open();
    return con;
}

app.MapGet("/echo", () => {
    return "Hello World!";
}).WithName("Echo").WithOpenApi();

app.MapGet("/GetAllList",()=>{
    var conn = ConnectDB();
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
    return JsonSerializer.Serialize(new Result{Code = 200,Message = JsonSerializer.Serialize(items,jsonSerializerOptions)},jsonSerializerOptions);
}).WithName("GetList").WithOpenApi();

app.MapGet("/add_data", (string name,string tags,int count) =>{
    // クエリパラメータの存在を確認し、存在しない場合はエラーメッセージを表示
    if (name == null || tags == null || count == null){
        return JsonSerializer.Serialize(new Result{Code = 400, Message = "Query Parameter is not found.\nNeed name,tag,count Query Parameter."});
    }
    var conn = ConnectDB();
    MySqlTransaction trans = null;
    DateTime localDate = DateTime.Now;
    string sqlCmd = $"INSERT INTO Items (name, tag, quantity, created_at, updated_at)VALUES ('{name}', '{tags}', {count}, '{localDate.ToString("yyyy-MM-dd HH:mm:ss")}', '{localDate.ToString("yyyy-MM-dd HH:mm:ss")}');";
    MySqlCommand cmd = new MySqlCommand(sqlCmd, conn);
    try{
        trans = cmd.Connection.BeginTransaction(IsolationLevel.ReadCommitted);
        cmd.ExecuteNonQuery();
        trans.Commit();
    }catch (MySqlException mse){
        trans.Rollback();
        return JsonSerializer.Serialize(new Result{Code = Convert.ToInt32(mse.Code), Message = $"{mse.Message}\nINSERT INTO Items (name, tag, quantity, created_at, updated_at)VALUES ('{name}', '{tags}', {count}, '{localDate.ToString("yyyy-MM-dd HH:mm:ss")}', '{localDate.ToString("yyyy-MM-dd HH:mm:ss")}')"},jsonSerializerOptions);
        // 例外発生時はロールバック
    }
    return JsonSerializer.Serialize(new Result{Code = 200, Message = "Success.\nData is added."},jsonSerializerOptions);
}).WithName("AddData").WithOpenApi();

app.MapGet("/delete_data", (int id) =>{
    // クエリパラメータの存在を確認し、存在しない場合はエラーメッセージを表示
    if (id == null){
        return JsonSerializer.Serialize(new Result{Code = 400, Message = "Query Parameter is not found.\nNeed id Query Parameter."});
    }
    var conn = ConnectDB();

    string sqlCmd = $"DELETE FROM Items WHERE id = {id};";
    MySqlTransaction trans = null;

    // 削除クエリの開始
    MySqlCommand cmd = new MySqlCommand(sqlCmd, conn);

    try{
        // 選択中のIDを用いて、ステークホルダーのセット
        cmd.Parameters.AddWithValue("id", id);

        // トランザクション監視開始
        trans = cmd.Connection.BeginTransaction(IsolationLevel.ReadCommitted);

        // SQL実行
        cmd.ExecuteNonQuery();

        // DBをコミット
        trans.Commit();
    }
    catch (MySqlException mse){
        trans.Rollback();                   // 例外発生時はロールバック
        return JsonSerializer.Serialize(new Result{Code = Convert.ToInt32(mse.Code), Message = $"{mse.Message}\nDELETE FROM Items WHERE id = {id}"},jsonSerializerOptions);
    }
    finally{
        // 接続はクローズする
        cmd.Connection.Close();
    }


    return JsonSerializer.Serialize(new Result{Code = 200, Message = "Success.\nData is deleted."},jsonSerializerOptions);
}
).WithName("DeleteData").WithOpenApi();

app.Run();


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