import os
import re

directory = r"C:\WarePro\QuanLyHangHoa\Views"

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # We want to find <Button ...> ... </Button>
    # and inside them, remove Style="{StaticResource BodyText}" or Style="{StaticResource LabelText}"
    # on <TextBlock ... /> elements.
    
    # Because XML parsing might be complex, we can just use regex to find Button blocks
    # and replace inside them.
    
    def replace_in_button(match):
        btn_content = match.group(0)
        # Remove Style="{StaticResource BodyText}" or LabelText
        btn_content = re.sub(r'\s*Style="\{StaticResource (BodyText|LabelText|TypographyCaption|TypographyLabel)\}"', '', btn_content)
        return btn_content

    # Regex for <Button ...> ... </Button> or <Button .../>
    # Non-greedy match, allowing newlines
    new_content = re.sub(r'<Button\b[^>]*>.*?</Button>', replace_in_button, content, flags=re.DOTALL)
    
    if new_content != content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Modified: {os.path.basename(filepath)}")

for root, _, files in os.walk(directory):
    for file in files:
        if file.endswith('.xaml'):
            process_file(os.path.join(root, file))

print("Done.")
