using MongoDB.Driver;
using RhemaBibleAppServerless.Application.Persistence;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class MongoOtpRepository(IMongoDbService mongo) : IOtpRepository
{
  public Task InvalidateActiveOtpsAsync(string email, OtpType type, CancellationToken cancellationToken = default)
  {
    var filter = Builders<OtpCode>.Filter.Eq(o => o.Email, email) &
                 Builders<OtpCode>.Filter.Eq(o => o.Type, type) &
                 Builders<OtpCode>.Filter.Eq(o => o.IsUsed, false);
    var update = Builders<OtpCode>.Update
      .Set(o => o.IsUsed, true)
      .Set(o => o.UsedAt, DateTime.UtcNow);
    return mongo.OtpCode.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
  }

  public Task InsertAsync(OtpCode code, CancellationToken cancellationToken = default) =>
    mongo.OtpCode.InsertOneAsync(code, cancellationToken: cancellationToken);

  public async Task<OtpCode?> FindByCodeAndTypeAsync(string code, OtpType type, CancellationToken cancellationToken = default)
  {
    return await mongo.OtpCode.Find(o => o.Code == code && o.Type == type).FirstOrDefaultAsync(cancellationToken);
  }

  public Task IncrementAttemptsAsync(string otpId, CancellationToken cancellationToken = default) =>
    mongo.OtpCode.UpdateOneAsync(
      o => o.Id == otpId,
      Builders<OtpCode>.Update.Inc(o => o.Attempts, 1),
      cancellationToken: cancellationToken);

  public Task MarkUsedAsync(string otpId, CancellationToken cancellationToken = default)
  {
    var update = Builders<OtpCode>.Update
      .Set(o => o.IsUsed, true)
      .Set(o => o.UsedAt, DateTime.UtcNow);
    return mongo.OtpCode.UpdateOneAsync(o => o.Id == otpId, update, cancellationToken: cancellationToken);
  }
}
