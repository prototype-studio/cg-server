using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CG.Users;

namespace CG.Databases;

public class DynamoDbClient : IDatabaseClient
{
    private readonly IAmazonDynamoDB ddbClient = new AmazonDynamoDBClient(RegionEndpoint.USEast1);
    
    public async Task<bool> UserExists(string username, string? sha256 = null, string? token = null)
    {
        var getUserRequest = new GetItemRequest()
        { 
            TableName = DatabaseConstants.USERS_TABLE,
            Key = new Dictionary<string, AttributeValue>
            {
                { DatabaseConstants.USERNAME_FIELD, new AttributeValue { S = username } }
            },
        };
        var getUserResponse = await ddbClient.GetItemAsync(getUserRequest);
        bool found = getUserResponse.Item is {Count: > 0};
        if(found && sha256 != null)
        {
            found = getUserResponse.Item.TryGetValue(DatabaseConstants.PASSWORD_FIELD, out var passwordAttribute) && sha256 == passwordAttribute.S;
        }
        if(found && token != null)
        {
            found = getUserResponse.Item.TryGetValue(DatabaseConstants.TOKEN_FIELD, out var tokenAttribute) && token == tokenAttribute.S;
        } 
        return found;
    }

    public async Task<string> RegisterUser(string username, string password)
    {
        var userAlreadyExists = await UserExists(username);
        if (userAlreadyExists)
        {
            throw new Exception($"User with username {username} already exists.");
        }
        var newToken = Guid.NewGuid().ToString();
        var writeItems = new TransactWriteItemsRequest()
        {
            TransactItems =
            [
                new TransactWriteItem()
                {
                    Put = new Put()
                    {
                        TableName = DatabaseConstants.USERS_TABLE,
                        Item = new Dictionary<string, AttributeValue>()
                        {
                            {DatabaseConstants.USERNAME_FIELD, new AttributeValue {S = username}},
                            {DatabaseConstants.PASSWORD_FIELD, new AttributeValue {S = Cryptography.GenerateSha256Hash(password)}},
                            {DatabaseConstants.TOKEN_FIELD, new AttributeValue {S = newToken}},
                            {DatabaseConstants.CURRENCY_FIELD, new AttributeValue {N = "0"}},
                        },
                    }
                },

                new TransactWriteItem()
                {
                    Put = new Put()
                    {
                        TableName = DatabaseConstants.TOKENS_TABLE,
                        Item = new Dictionary<string, AttributeValue>()
                        {
                            {DatabaseConstants.TOKEN_FIELD, new AttributeValue {S = newToken}},
                            {DatabaseConstants.USERNAME_FIELD, new AttributeValue {S = username}},
                            {DatabaseConstants.TTL_FIELD, new AttributeValue {N = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600).ToString()}},
                        },
                    }
                }
            ]
        };
        await ddbClient.TransactWriteItemsAsync(writeItems);
        return newToken;
    }

    public async Task<string> LoginUser(string username, string password)
    {
        var credentialsValid = await UserExists(username, Cryptography.GenerateSha256Hash(password));
        if (!credentialsValid)
        {
            throw new Exception("Invalid username or password");
        }
        
        var newToken = Guid.NewGuid().ToString();
        var writeItems = new TransactWriteItemsRequest()
        {
            TransactItems =
            [
                new TransactWriteItem()
                {
                    Update = new Update()
                    {
                        TableName = DatabaseConstants.USERS_TABLE,
                        Key = new Dictionary<string, AttributeValue>()
                        {
                            {DatabaseConstants.USERNAME_FIELD, new AttributeValue {S = username}},
                        },
                        UpdateExpression = "SET #t = :newToken",
                        ExpressionAttributeNames = new Dictionary<string, string>()
                        {
                            {"#t", DatabaseConstants.TOKEN_FIELD}
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                        {
                            {":newToken", new AttributeValue {S = newToken}}
                        }
                    },
                },

                new TransactWriteItem()
                {
                    Put = new Put()
                    {
                        TableName = DatabaseConstants.TOKENS_TABLE,
                        Item = new Dictionary<string, AttributeValue>()
                        {
                            {DatabaseConstants.TOKEN_FIELD, new AttributeValue {S = newToken}},
                            {DatabaseConstants.USERNAME_FIELD, new AttributeValue {S = username}},
                            {DatabaseConstants.TTL_FIELD, new AttributeValue {N = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600).ToString()}},
                        },
                    }
                }
            ]
        };
        await ddbClient.TransactWriteItemsAsync(writeItems);
        return newToken;
    }
    
    public async Task<User> GetUserFromToken(string token)
    {
        var getTokenRequest = new GetItemRequest()
        { 
            TableName = DatabaseConstants.TOKENS_TABLE,
            Key = new Dictionary<string, AttributeValue>
            {
                { DatabaseConstants.TOKEN_FIELD, new AttributeValue { S = token } }
            },
        };
        var getTokenResponse = await ddbClient.GetItemAsync(getTokenRequest);
        if (getTokenResponse.Item is not { Count: > 0 } || !getTokenResponse.Item.TryGetValue(DatabaseConstants.USERNAME_FIELD, out var usernameAttribute))
        {
            throw new Exception("Invalid token");
        }
        var username = usernameAttribute.S;
        
        var getUserRequest = new GetItemRequest()
        { 
            TableName = DatabaseConstants.USERS_TABLE,
            Key = new Dictionary<string, AttributeValue>
            {
                { DatabaseConstants.USERNAME_FIELD, new AttributeValue { S = username } }
            },
        };
        var getUserResponse = await ddbClient.GetItemAsync(getUserRequest);
        if (getUserResponse.Item is not { Count: > 0 } ||
            !getUserResponse.Item.TryGetValue(DatabaseConstants.TOKEN_FIELD, out var tokenAttribute) ||
            !getUserResponse.Item.TryGetValue(DatabaseConstants.CURRENCY_FIELD, out var currencyAttribute))
        {
            throw new Exception("User data corrupted");
        }

        if (tokenAttribute.S != token)
        {
            throw new Exception("Invalid token");
        }

        var userItem = getUserResponse.Item;
        // extract fields from user item and convert to Dictionary<string, string>
        var userAttributes = new Dictionary<string, string>();
        foreach (var kvp in userItem)
        {
            if(kvp.Key is DatabaseConstants.PASSWORD_FIELD or DatabaseConstants.TOKEN_FIELD)
            {
                continue; // skip password and token fields
            }
            if (kvp.Value.S != null)
            {
                userAttributes[kvp.Key] = kvp.Value.S;
            }
            else if (kvp.Value.N != null)
            {
                userAttributes[kvp.Key] = kvp.Value.N;
            }
        }
        return new User(userAttributes);
    }

    public async Task AuthenticateConnection(string username, string token, string connectionId)
    {
        var transactRequest = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Update = new Update
                    {
                        TableName = DatabaseConstants.USERS_TABLE,
                        Key = new Dictionary<string, AttributeValue>()
                        {
                            {
                                DatabaseConstants.USERNAME_FIELD, new AttributeValue {S = username}
                            },
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                        {
                            {$":{DatabaseConstants.CONNECTION_ID_FIELD}", new AttributeValue {S = connectionId}},
                            {$":{DatabaseConstants.TOKEN_FIELD}", new AttributeValue {S = token}}
                        },
                        ConditionExpression = $"attribute_not_exists({DatabaseConstants.CONNECTION_ID_FIELD}) AND {DatabaseConstants.TOKEN_FIELD} = :{DatabaseConstants.TOKEN_FIELD}",
                        UpdateExpression = $"SET {DatabaseConstants.CONNECTION_ID_FIELD} = :{DatabaseConstants.CONNECTION_ID_FIELD}",
                        ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE,
                    },
                },

                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = DatabaseConstants.CONNECTIONS_TABLE,
                        Item = new Dictionary<string, AttributeValue>()
                        {
                            [DatabaseConstants.CONNECTION_ID_FIELD] = new  AttributeValue {S = connectionId},
                            [DatabaseConstants.USERNAME_FIELD] = new  AttributeValue {S = username},
                            [DatabaseConstants.TOKEN_FIELD] = new  AttributeValue {S = token},
                        },
                    }
                },

                new TransactWriteItem
                {
                    ConditionCheck = new ConditionCheck()
                    {
                        TableName = DatabaseConstants.TOKENS_TABLE,
                        Key = new Dictionary<string, AttributeValue>()
                        {
                            { DatabaseConstants.TOKEN_FIELD, new AttributeValue { S = token } }
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                        {
                            { $":{DatabaseConstants.USERNAME_FIELD}", new AttributeValue { S = username } }
                        },
                        ConditionExpression = $"attribute_exists({DatabaseConstants.TOKEN_FIELD}) AND {DatabaseConstants.USERNAME_FIELD} = :{DatabaseConstants.USERNAME_FIELD}",
                    },
                }
            ],
        };

        await ddbClient.TransactWriteItemsAsync(transactRequest);
    }

    public async Task RemoveConnection(string username, string connectionId)
    {
        var transactRequest = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Update = new Update
                    {
                        TableName = DatabaseConstants.USERS_TABLE,
                        Key = new Dictionary<string, AttributeValue>()
                        {
                            {
                                DatabaseConstants.USERNAME_FIELD, new AttributeValue {S = username}
                            },
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                        {
                            {$":{DatabaseConstants.CONNECTION_ID_FIELD}", new AttributeValue {S = connectionId}},
                        },
                        ConditionExpression = $"attribute_exists({DatabaseConstants.CONNECTION_ID_FIELD}) AND {DatabaseConstants.CONNECTION_ID_FIELD} = :{DatabaseConstants.CONNECTION_ID_FIELD}",
                        UpdateExpression = $"REMOVE {DatabaseConstants.CONNECTION_ID_FIELD}",
                        ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE,
                    },
                },
                new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = DatabaseConstants.CONNECTIONS_TABLE,
                        Key = new Dictionary<string, AttributeValue>()
                        {
                            [DatabaseConstants.CONNECTION_ID_FIELD] = new  AttributeValue {S = connectionId},
                        },
                    }
                },
            ],
        };

        await ddbClient.TransactWriteItemsAsync(transactRequest);
    }
}