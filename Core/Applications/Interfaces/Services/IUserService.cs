using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Applications.Interfaces.Services;

public interface IUserService
{
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task<bool> DeleteUserAsync(Guid userId);
    Task<bool> DeactivateUserAsync(Guid userId);
    Task<bool> ActivateUserAsync(Guid userId);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task<bool> ResetPasswordAsync(string email, string newPassword);
    Task<IEnumerable<User>> GetUsersAsync(int page = 1, int pageSize = 20);
    Task<bool> EmailExistsAsync(string email);
    Task UpdateLastLoginAsync(Guid userId);
}