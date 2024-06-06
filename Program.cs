using System.Text.Json;
using MySql.Data.MySqlClient;
using System.Data;
using System.Text.Encodings.Web;
using NSwag.AspNetCore;


class Program {
    public static void Main(string[] args){
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApiDocument(config =>{
            config.DocumentName = "IwaiDBAPI";
            config.Title = "IwaiDBAPI v1";
            config.Version = "v1";
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment()){
            app.UseOpenApi();
            app.UseSwaggerUi(config =>{
                config.DocumentTitle = "IwaiDBAPI";
                config.Path = "/swagger";
                config.DocumentPath = "/swagger/{documentName}/swagger.json";
                config.DocExpansion = "list";
            });
        }

        app.UseHttpsRedirection();

        var jsonSerializerOptions = new JsonSerializerOptions{
            WriteIndented = true, // not necessary, but more readable
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        app.MapGet("/echo", () => {
            return "Hello World!";
        }).WithName("Echo").WithOpenApi();

        app.MapPost("/update_from_id",(UpdateItemData body)=>{
            // Get query parameters
            var id = body.Id;
            var quantity = body.Quantity;
            // クエリパラメータの存在を確認し、存在しない場合はエラーメッセージを表示
            if (id == null || quantity == null){
                return JsonSerializer.Serialize(new Result{Code = 400, Message = "Query Parameter is not found.\nNeed id,quantity Query Parameter."});
            }
            DateTime localDate = DateTime.Now;
            if(UpdateDB(id, null, quantity)){
                return JsonSerializer.Serialize(new Result{Code = 200, Message = "Success.\nData is updated."},jsonSerializerOptions);
            }else{
                return JsonSerializer.Serialize(new Result{Code = 500, Message = "Failed.\nData is not updated."},jsonSerializerOptions);
            }
        }).WithName("UpdateFromID").WithOpenApi();

        app.MapGet("/search_from_name", (string name,int limit = 10) => {
            Item[] ret = null;
            try{
                ret = SearchDB(null, name, null, null, null, limit);
            }catch(Exception e){
                return JsonSerializer.Serialize(new Result{Code = 500,Message = e.Message},jsonSerializerOptions);
            }
            if(ret == null){
                return JsonSerializer.Serialize(new Result{Code = 404,Message = "Data is not found."},jsonSerializerOptions);
            }
            return JsonSerializer.Serialize(new Result{Code = 200,Message = JsonSerializer.Serialize(ret,jsonSerializerOptions).ToString()},jsonSerializerOptions);

        }).WithName("SearchFromName").WithOpenApi();


        app.MapGet("/search_from_tag",(string tag,int limit = 10)=>{
            Item[] ret = null;
            try{
                ret = SearchDB(null, null, tag, null, null, limit);
            }catch(Exception e){
                return JsonSerializer.Serialize(new Result{Code = 500,Message = e.Message},jsonSerializerOptions);
            }
            if(ret == null){
                return JsonSerializer.Serialize(new Result{Code = 404,Message = "Data is not found."},jsonSerializerOptions);
            }
            return JsonSerializer.Serialize(new Result{Code = 200,Message = JsonSerializer.Serialize(ret,jsonSerializerOptions).ToString()},jsonSerializerOptions);
        }).WithName("SearchFromTag").WithOpenApi();

        app.MapGet("/get_all_list",()=>{
                Item[] ret = null;
            try{
                ret = SearchDB(null, null, null, null, null ,null);
            }catch(Exception e){
                return JsonSerializer.Serialize(new Result{Code = 500,Message = e.Message},jsonSerializerOptions);
            }
            if(ret == null){
                return JsonSerializer.Serialize(new Result{Code = 404,Message = "Data is not found."},jsonSerializerOptions);
            }
            return JsonSerializer.Serialize(new Result{Code = 200,Message = JsonSerializer.Serialize(ret,jsonSerializerOptions).ToString()},jsonSerializerOptions);
        }).WithName("GetList").WithOpenApi();

        app.MapPost("/add_data", (GetItemData body) =>{
            // Get query parameters
            var name = body.Name;
            var tags = body.Tag;
            var quantity = body.Quantity;
            // Check if any query parameter is missing
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(tags) || quantity <= 0){
                return JsonSerializer.Serialize(new Result { Code = 400, Message = "Missing or invalid query parameter(s)." });
            }
            var conn = ConnectDB();
            MySqlTransaction trans = null;
            DateTime localDate = DateTime.Now;
            string sqlCmd = $"INSERT INTO Items (name, tag, quantity, created_at, updated_at) VALUES ('{name}', '{tags}', {quantity}, '{localDate.ToString("yyyy-MM-dd HH:mm:ss")}', '{localDate.ToString("yyyy-MM-dd HH:mm:ss")}');";
            MySqlCommand cmd = new MySqlCommand(sqlCmd, conn);
            try{
                trans = cmd.Connection.BeginTransaction(IsolationLevel.ReadCommitted);
                cmd.ExecuteNonQuery();
                trans.Commit();
            }
            catch (MySqlException mse){
                trans?.Rollback();
                return JsonSerializer.Serialize(new Result { Code = Convert.ToInt32(mse.Number), Message = $"{mse.Message}\n{sqlCmd}" });
            }
            return JsonSerializer.Serialize(new Result { Code = 200, Message = "Success.\nData is added." });
        }).WithName("AddData").WithOpenApi();

        app.MapGet("/delete_fromID", (int id) =>{
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
        }).WithName("DeleteDataFromID").WithOpenApi();

        app.Run();
    }

    static MySqlConnection ConnectDB(){
        var con = new MySqlConnection("server=localhost;user id=root;password=root;database=IwaiDB");
        con.Open();
        return con;
    }

    //更新用の関数
    static bool UpdateDB(int? id, string name, int? quantity){
        var conn = ConnectDB();
        MySqlTransaction trans = null;

        // 更新するアイテムが存在するかどうかを確認するためのSQLクエリ
        string checkExistenceSql = "";
        if (id.HasValue){
            checkExistenceSql = $"SELECT COUNT(*) FROM Items WHERE id = {id}";
        }
        else if (!string.IsNullOrEmpty(name)){
            checkExistenceSql = $"SELECT COUNT(*) FROM Items WHERE name = '{name}'";
        }

        if (!string.IsNullOrEmpty(checkExistenceSql)){
            using (var cmdCheck = new MySqlCommand(checkExistenceSql, conn)){
                int count = Convert.ToInt32(cmdCheck.ExecuteScalar());

                if (count > 0 && quantity.HasValue && quantity.Value > 0){
                    string updateSql;
                    if (id.HasValue){
                        updateSql = $"UPDATE Items SET quantity = {quantity}, updated_at = NOW() WHERE id = {id}";
                    }
                    else{
                        updateSql = $"UPDATE Items SET quantity = {quantity}, updated_at = NOW() WHERE name = '{name}'";
                    }

                    using (var cmdUpdate = new MySqlCommand(updateSql, conn)){
                        try{
                            trans = cmdUpdate.Connection.BeginTransaction(IsolationLevel.ReadCommitted);
                            cmdUpdate.ExecuteNonQuery();
                            trans.Commit();
                            return true;
                        }
                        catch (MySqlException){
                            trans?.Rollback();
                            return false;
                        }
                    }
                }
                else{
                    return false;
                }
            }
        }
        else{
            return false;
        }
    }

    //検索用の関数
    static Item[] SearchDB(int? id, string name, string tag, DateTime? createdAt, DateTime? updatedAt,int? limit){
        var conn = ConnectDB();
        DataTable tbl = new DataTable();

        // SQLクエリの基本部分
        string baseSql = "SELECT * FROM Items";

        // 条件を格納するリスト
        List<MySqlParameter> parameters = new List<MySqlParameter>();
        List<string> conditions = new List<string>();

        // 各フィールドに基づく条件を追加
        if (id.HasValue){
            conditions.Add("@id = @idParam");
            parameters.Add(new MySqlParameter("@idParam", id.Value));
        }
        if (!string.IsNullOrWhiteSpace(name)){
            conditions.Add("name LIKE @nameParam");
            parameters.Add(new MySqlParameter("@nameParam", "%" + name + "%"));
        }
        if (!string.IsNullOrWhiteSpace(tag)){
            conditions.Add("tag LIKE @tagParam");
            parameters.Add(new MySqlParameter("@tagParam", "%" + tag + "%"));
        }
        if (createdAt.HasValue){
            conditions.Add("created_at >= @createdAtParam");
            parameters.Add(new MySqlParameter("@createdAtParam", createdAt.Value));
        }
        if (updatedAt.HasValue){
            conditions.Add("updated_at <= @updatedAtParam");
            parameters.Add(new MySqlParameter("@updatedAtParam", updatedAt.Value));
        }

        // 条件が存在する場合、WHERE句を追加
        if (conditions.Any()){
            string whereClause = string.Join(" AND ", conditions);
            baseSql += $" WHERE {whereClause}";
            if(limit.HasValue){
                baseSql += $" LIMIT {limit}";
            }
            baseSql += ";";

            // パラメータを含むSQLクエリの実行
            using (var cmd = new MySqlCommand(baseSql, conn)){
                foreach (var param in parameters){
                    cmd.Parameters.Add(param);
                }
                MySqlDataAdapter dataAdp = new MySqlDataAdapter(cmd);
                dataAdp.Fill(tbl);
            }
        } else {
            // 条件がない場合、全データを取得
            MySqlDataAdapter dataAdp = new MySqlDataAdapter(baseSql, conn);
            dataAdp.Fill(tbl);
        }

        // DataTable を List<Item> に変換
        Item[] items = new Item[tbl.Rows.Count];
        for(int i = 0; i < tbl.Rows.Count; i++){
            items[i] = new Item{
                Id = int.Parse(tbl.Rows[i]["id"].ToString()),
                Name = tbl.Rows[i]["name"].ToString(),
                Tag = tbl.Rows[i]["tag"].ToString(),
                Quantity = int.Parse(tbl.Rows[i]["quantity"].ToString()),
                CreatedAt = DateTime.Parse(tbl.Rows[i]["created_at"].ToString()),
                UpdatedAt = DateTime.Parse(tbl.Rows[i]["updated_at"].ToString())
            };
        }
        return items;
    }

}



//メモ
//名前(VARCHAR(40)),タグ(TEXT),数量(smallINT),登録日時(DATETIME),更新日時(DATETIME)

class Result{
    public int Code {get;set;}
    public string Message {get;set;}
}

class GetItemData{
    public string Name { get; set; }
    public string Tag { get; set; }
    public int Quantity { get; set; }

}

class UpdateItemData{
    public int Id { get; set; }
    public int Quantity { get; set; }
}

class Item{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Tag { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}