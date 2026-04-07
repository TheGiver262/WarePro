import urllib.request
import re
import json

url = 'https://chatgpt.com/share/69d49349-1524-8398-9429-1a40e376cd06'
req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
try:
    html = urllib.request.urlopen(req).read().decode('utf-8')
    codes = re.findall(r'<code[^>]*>(.*?)</code>', html, re.DOTALL)
    if codes:
        with open('chat_codes.txt', 'w', encoding='utf-8') as f:
            for c in codes:
                f.write(c + '\n\n---\n\n')
        print(f'Found {len(codes)} code blocks and saved to chat_codes.txt')
    else:
        print('Could not find any code blocks.')
except Exception as e:
    print('Error:', e)
