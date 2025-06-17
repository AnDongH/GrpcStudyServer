namespace GrpcStudyServer.Databases;

public class DBOptions {

    public const string DbConfig = "ConnectionStrings";

    public string PgSql { get; set; }
    public string Redis { get; set; }
    
}