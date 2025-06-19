#include <stdio.h>
#include <string.h>

int main() {
    char password[50];
    char correct[] = "secret123";
    
    printf("Welcome to the crackme challenge!\n");
    printf("Enter the password: ");
    scanf("%s", password);
    
    if (strcmp(password, correct) == 0) {
        printf("Congratulations! You found the flag: flag{simple_crackme_solved_2024}\n");
    } else {
        printf("Wrong password. Try again!\n");
    }
    
    return 0;
} 