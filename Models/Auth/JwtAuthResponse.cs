namespace Models.Auth
{
    public class JwtAuthResponse
    {
        public string Token { get; set; }
        public string RefreshToken { get; set; }
    }
}
