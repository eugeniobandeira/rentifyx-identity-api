namespace RentifyxIdentity.Domain.Interfaces.Users;

public interface IPasswordHasher
{
    string Hash(string plaintext);
    bool Verify(string plaintext, string hash);
}
