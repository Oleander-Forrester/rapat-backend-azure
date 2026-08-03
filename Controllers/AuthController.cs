using rapat_backend.DTOs.Auth;
using rapat_backend.Helpers;
using rapat_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace rapat_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IAuthService auth, IPushNotificationService pushNotificationService) : ControllerBase
    {
        private readonly IAuthService _auth = auth;
        private readonly IPushNotificationService _pushNotificationService = pushNotificationService;

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var sanitizedDto = SanitizerHelper.EncodeObject(dto);
            if (sanitizedDto == null) return NotFound();

            var res = await _auth.AuthenticateAsync(sanitizedDto);
            if (res?.Token == null) return Unauthorized(new { message = res?.ErrorMessage });
            if (!string.IsNullOrEmpty(res?.ErrorMessage)) return BadRequest(new { message = res?.ErrorMessage });
            return Ok(res);
        }

        [Authorize]
        [HttpPost("getpermission")]
        public async Task<IActionResult> GetPermission([FromBody] PermissionRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var sanitizedDto = SanitizerHelper.EncodeObject(dto);
            if (sanitizedDto == null) return NotFound();

            var res = await _auth.GetPermissionAsync(sanitizedDto);
            if (res?.Token == null) return Unauthorized(new { message = res?.ErrorMessage });
            if (!string.IsNullOrEmpty(res?.ErrorMessage)) return BadRequest(new { message = res?.ErrorMessage });
            return Ok(res);
        }

        [Authorize]
        [HttpPost("getmenu")]
        public async Task<IActionResult> GetMenu([FromBody] PermissionRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var username = User.FindFirstValue("namaakun");
            var appId = User.FindFirstValue("idapp");
            var roleId = User.FindFirstValue("idrole");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(roleId))
            {
                return Unauthorized();
            }

            var sanitizedDto = SanitizerHelper.EncodeObject(dto);
            if (sanitizedDto == null) return NotFound();

            var res = await _auth.GetMenuAsync(sanitizedDto);
            if (!string.IsNullOrEmpty(res?.ErrorMessage)) return BadRequest(new { message = res?.ErrorMessage });
            return Ok(res);
        }
        [Authorize]
        [HttpPost("SavePushToken")]
        public async Task<IActionResult> SavePushToken([FromBody] SavePushTokenRequest dto)
        {
            var npk = User.FindFirst("namaakun")?.Value;
            if (string.IsNullOrEmpty(npk)) return Unauthorized(new { message = "Token tidak valid atau NPK tidak ditemukan." });

            var success = await _pushNotificationService.SavePushTokenAsync(npk, dto.ExpoPushToken);
            if (!success) return StatusCode(500, new { message = "Gagal menyimpan push token." });

            return Ok(new { message = "Push token berhasil disimpan." });
        }
    }
}
