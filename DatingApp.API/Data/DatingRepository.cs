using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.API.Data
{
    public class DatingRepository : IDatingRepository
    {
        private readonly DataContext _context;
        public DatingRepository(DataContext context)
        {
            _context = context;
        }
        public void Add<T>(T entity) where T : class
        {
            _context.Add(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
            _context.Remove(entity);
        }

        public async Task<Like> GetLike(int userId, int recipientId)
        {
            return await _context.Likes.FirstOrDefaultAsync(u =>
                 u.LikerId == userId && u.LikeeId == recipientId);
        }

        public async Task<Photo> GetMainPhotoForUser(int userId)
        {
            var mainPhoto = await _context.Photos.Where(u => u.UserId == userId)
                    .FirstOrDefaultAsync(p => p.IsMain);
            
            return mainPhoto;
        }

        public async Task<Photo> GetPhoto(int id)
        {
            var photo = await _context.Photos.FirstOrDefaultAsync(p => p.Id == id);
            
            return photo;
        }

        public async Task<User> GetUser(int id)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == id);
            
            return user;
        }

        public async Task<PagedList<User>> GetUsers(UserParameters userParameters)
        {
            var users = _context.Users
                .OrderByDescending(u => u.LastActive).AsQueryable();

            users = users.Where(u => u.Id != userParameters.UserId);

            users = users.Where(u => u.Gender == userParameters.Gender);

            if(userParameters.Likers)
            {
                var userLikers = await GetUserLikes(userParameters.UserId, userParameters.Likers);
                users = users.Where(u => userLikers.Contains(u.Id));
            }

            if(userParameters.Likees)
            {
                var userLikees = await GetUserLikes(userParameters.UserId, userParameters.Likers);
                users = users.Where(u => userLikees.Contains(u.Id));
            }

            if(userParameters.MinAge != 18 || userParameters.MaxAge != 99)
            {
                var minDateOfBirth = DateTime.Today.AddYears(-userParameters.MaxAge - 1);
                var maxDateOfBirth = DateTime.Today.AddYears(-userParameters.MinAge);

                users = users.Where(u => u.DateOfBirth >= minDateOfBirth && u.DateOfBirth <= maxDateOfBirth);
            }
            
            if(!string.IsNullOrEmpty(userParameters.OrderBy))
            {
                switch(userParameters.OrderBy)
                {
                    case "created": 
                        users = users.OrderByDescending(u => u.Created);
                        break;
                        default:
                        users = users.OrderByDescending(u => u.LastActive);
                        break;
                }
            }

            return await PagedList<User>.CreateAsync(users, userParameters.PageNumber, userParameters.PageSize);
        }

        private async Task<IEnumerable<int>> GetUserLikes(int id, bool likers)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            if(likers)
            {
                return user.Likers.Where(u => u.LikeeId == id).Select(i => i.LikerId);
            }
            else
            {
                return user.Likees.Where(u => u.LikerId == id).Select(i => i.LikeeId);
            }
        }

        public async Task<bool> SaveAll()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<Message> GetMessage(int id)
        {
            return await _context.Messages.FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<PagedList<Message>> GetMessagesForUser(MessageParameters messageParameters)
        {
            var messages = _context.Messages
                .AsQueryable();

            switch(messageParameters.MessageContainer)
            {
                case "Inbox": 
                    messages = messages.Where(u => u.RecipientId == messageParameters.UserId 
                        && u.RecipientDeleted == false);
                    break;
                case "Outbox":
                    messages = messages.Where(u => u.SenderId == messageParameters.UserId 
                        && u.SenderDeleted == false);
                    break;
                default: 
                    messages = messages.Where(u => u.RecipientId == messageParameters.UserId 
                        && u.RecipientDeleted == false
                        && u.IsRead == false);
                    break;
            }

            messages = messages.OrderByDescending(d => d.MessageSent);

            return await PagedList<Message>.CreateAsync(messages, messageParameters.PageNumber, messageParameters.PageSize);
        }

        public async Task<IEnumerable<Message>> GetMessageThread(int userId, int recipientId)
        {
            var messages = await _context.Messages
                .Where(m => m.RecipientId == userId && m.RecipientDeleted == false
                    && m.SenderId == recipientId 
                    || m.RecipientId == recipientId && m.SenderId == userId
                    && m.SenderDeleted == false)
                .OrderByDescending(m => m.MessageSent)
                .ToListAsync();

            return messages;
        }
    }
}