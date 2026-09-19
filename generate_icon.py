import math
import os
from PIL import Image, ImageDraw, ImageFilter

def create_smartnotes_icon():
    size = 1024
    base = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    # Maximized Squircle canvas for prominent system tray & taskbar visibility
    pad = 18
    r = 230
    x0, y0, x1, y1 = pad, pad, size - pad, size - pad

    # 1. Base Dark Card
    card_mask = Image.new("L", (size, size), 0)
    cm_draw = ImageDraw.Draw(card_mask)
    cm_draw.rounded_rectangle([x0, y0, x1, y1], radius=r, fill=255)

    # Dark Obsidian Gradient Background (#131828 to #080B12)
    bg = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    bg_draw = ImageDraw.Draw(bg)
    for y in range(y0, y1 + 1):
        t = (y - y0) / max(1, (y1 - y0))
        r_c = int(19 * (1 - t) + 8 * t)
        g_c = int(24 * (1 - t) + 11 * t)
        b_c = int(40 * (1 - t) + 18 * t)
        bg_draw.line([(x0, y), (x1, y)], fill=(r_c, g_c, b_c, 255))

    card = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    card.paste(bg, (0, 0), mask=card_mask)

    # 2. Ambient Neon Glow inside the card (Amber / Emerald / Violet bloom)
    ambient_glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ag_draw = ImageDraw.Draw(ambient_glow)
    cx, cy = size // 2, size // 2

    for rad in range(460, 0, -5):
        t = rad / 460.0
        alpha = int((1.0 - t)**1.7 * 160)
        # Gradient from Amber #F59E0B to Violet #8B5CF6
        r_g = int(245 * t + 139 * (1 - t))
        g_g = int(158 * t + 92 * (1 - t))
        b_g = int(11 * t + 246 * (1 - t))
        ag_draw.ellipse([cx - rad, cy - rad, cx + rad, cy + rad], fill=(r_g, g_g, b_g, alpha))

    card_glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    card_glow.paste(ambient_glow, (0, 0), mask=card_mask)
    card = Image.alpha_composite(card, card_glow)

    # 3. Outer Border
    border_layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    b_draw = ImageDraw.Draw(border_layer)
    b_draw.rounded_rectangle([x0, y0, x1, y1], radius=r, outline=(42, 58, 86, 255), width=18)
    b_draw.rounded_rectangle([x0 + 5, y0 + 5, x1 - 5, y1 - 5], radius=r - 5, outline=(245, 158, 11, 100), width=6)
    card = Image.alpha_composite(card, border_layer)

    # 4. Sticky Note Pad + Pin vector art (Scaled up for bold prominence)
    note_w = 580
    note_h = 630
    nx0 = (size - note_w) // 2
    ny0 = (size - note_h) // 2 + 25
    nx1 = nx0 + note_w
    ny1 = ny0 + note_h
    fold = 110

    # Note shadow
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    note_poly = [
        (nx0, ny0),
        (nx1 - fold, ny0),
        (nx1, ny0 + fold),
        (nx1, ny1),
        (nx0, ny1)
    ]
    s_draw.polygon([(x + 12, y + 30) for (x, y) in note_poly], fill=(0, 0, 0, 190))
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=32))

    # Note body glow
    aura = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    au_draw = ImageDraw.Draw(aura)
    au_draw.polygon(note_poly, fill=(245, 158, 11, 230))
    aura = aura.filter(ImageFilter.GaussianBlur(radius=42))

    # Note Body with gradient
    note_layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    
    note_mask = Image.new("L", (size, size), 0)
    nm_draw = ImageDraw.Draw(note_mask)
    nm_draw.polygon(note_poly, fill=255)

    note_gradient = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ng_draw = ImageDraw.Draw(note_gradient)
    for y in range(ny0, ny1 + 1):
        t = (y - ny0) / max(1, (ny1 - ny0))
        # Radiant gold/amber gradient #FDE047 to #F59E0B
        r_n = int(254 * (1 - t) + 245 * t)
        g_n = int(240 * (1 - t) + 158 * t)
        b_n = int(138 * (1 - t) + 11 * t)
        ng_draw.line([(0, y), (size, y)], fill=(r_n, g_n, b_n, 255))

    note_content = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    note_content.paste(note_gradient, (0, 0), mask=note_mask)

    # Note ruled lines & checkboxes
    lines_draw = ImageDraw.Draw(note_content)
    line_color = (180, 83, 9, 175)
    check_color = (180, 83, 9, 215)

    # Header title line
    lines_draw.rounded_rectangle([nx0 + 60, ny0 + 145, nx0 + 330, ny0 + 168], radius=10, fill=check_color)
    
    # Task items
    for i, ly in enumerate([ny0 + 230, ny0 + 320, ny0 + 410, ny0 + 500]):
        # Checkbox
        lines_draw.rounded_rectangle([nx0 + 60, ly, nx0 + 96, ly + 36], radius=8, outline=check_color, width=5)
        if i < 2:
            # Check mark in first two
            lines_draw.line([(nx0 + 68, ly + 18), (nx0 + 78, ly + 28), (nx0 + 90, ly + 10)], fill=check_color, width=5)
        # Line text
        line_w = 380 if i % 2 == 0 else 300
        lines_draw.rounded_rectangle([nx0 + 120, ly + 8, nx0 + 120 + line_w, ly + 28], radius=8, fill=line_color)

    # Folded corner
    fold_poly = [
        (nx1 - fold, ny0),
        (nx1 - fold, ny0 + fold),
        (nx1, ny0 + fold)
    ]
    fold_mask = Image.new("L", (size, size), 0)
    fm_draw = ImageDraw.Draw(fold_mask)
    fm_draw.polygon(fold_poly, fill=255)
    
    fold_grad = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    fg_draw = ImageDraw.Draw(fold_grad)
    fg_draw.polygon(fold_poly, fill=(217, 119, 6, 255)) # Darker amber for corner shadow
    note_content.paste(fold_grad, (0, 0), mask=fold_mask)

    # Crisp Note Outline
    nc_draw = ImageDraw.Draw(note_content)
    nc_draw.polygon(note_poly, outline=(255, 255, 255, 240), width=8)
    nc_draw.line([(nx1 - fold, ny0), (nx1 - fold, ny0 + fold), (nx1, ny0 + fold)], fill=(255, 255, 255, 210), width=6)

    # 5. Glowing Pin at top center
    pin_layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    p_draw = ImageDraw.Draw(pin_layer)
    pin_cx = size // 2
    pin_cy = ny0 - 12
    
    # Pin shadow
    p_draw.ellipse([pin_cx - 28, pin_cy + 10, pin_cx + 28, pin_cy + 42], fill=(0, 0, 0, 150))
    # Pin glow
    pin_aura = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    pa_draw = ImageDraw.Draw(pin_aura)
    pa_draw.ellipse([pin_cx - 44, pin_cy - 44, pin_cx + 44, pin_cy + 44], fill=(16, 185, 129, 255))
    pin_aura = pin_aura.filter(ImageFilter.GaussianBlur(radius=20))

    # Pin head (Neon Emerald)
    p_draw.ellipse([pin_cx - 34, pin_cy - 34, pin_cx + 34, pin_cy + 34], fill=(16, 185, 129, 255), outline=(255, 255, 255, 245), width=6)
    p_draw.ellipse([pin_cx - 12, pin_cy - 18, pin_cx + 5, pin_cy - 1], fill=(255, 255, 255, 230)) # Pin highlight

    # Assemble
    result = Image.alpha_composite(card, shadow)
    result = Image.alpha_composite(result, aura)
    result = Image.alpha_composite(result, note_content)
    result = Image.alpha_composite(result, pin_aura)
    result = Image.alpha_composite(result, pin_layer)

    return result

if __name__ == "__main__":
    os.makedirs("Assets", exist_ok=True)
    icon_img = create_smartnotes_icon()
    icon_img.save("Assets/app.png", format="PNG")
    
    # High-quality multi-res ICO including Windows High-DPI tray sizes (16, 20, 24, 32, 48, 64, 128, 256)
    sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (20, 20), (16, 16)]
    icon_img.save("Assets/app.ico", format="ICO", sizes=sizes)
    print("Generated Assets/app.png and Assets/app.ico successfully!")
