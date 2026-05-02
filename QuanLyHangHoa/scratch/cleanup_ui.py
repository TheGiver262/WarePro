import os
import re

def cleanup_xaml(directory):
    patterns = [
        r'\s+Height="42"',
        r'\s+VerticalContentAlignment="Center"',
        r'\s+VerticalContentAlignment="Stretch"',
    ]
    
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".xaml"):
                path = os.path.join(root, file)
                with open(path, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                original_content = content
                for pattern in patterns:
                    content = re.sub(pattern, '', content)
                
                if content != original_content:
                    with open(path, 'w', encoding='utf-8') as f:
                        f.write(content)
                    print(f"Cleaned: {file}")

if __name__ == "__main__":
    cleanup_xaml("QuanLyHangHoa/Views")
